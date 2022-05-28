﻿// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using WPFUI.Common;
using WPFUI.Controls.Interfaces;

namespace WPFUI.Controls.Navigation;

/// <summary>
/// Base implementation for the navigation view.
/// </summary>
public abstract class NavigationBase : System.Windows.Controls.Control, INavigation
{
    private bool _firstPagePresented = false;

    /// <summary>
    /// An identifier that is changed with each navigation.
    /// </summary>
    internal long CurrentActionIdentifier { get; private set; }

    /// <summary>
    /// Identifier of the navigation service item currently in use.
    /// </summary>
    internal int CurrentlyNavigatedServiceItem { get; private set; }

    /// <summary>
    /// Identifies current Frame process.
    /// </summary>
    internal readonly EventIdentifier _eventIdentifier;

    /// <summary>
    /// <see cref="Items"/> and <see cref="Footer"/> mirror with cached page contents.
    /// </summary>
    internal IDictionary<int, NavigationServiceItem> _navigationServiceItems;

    /// <summary>
    /// Property for <see cref="Items"/>.
    /// </summary>
    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(nameof(Items),
        typeof(ObservableCollection<INavigationControl>), typeof(NavigationBase),
        new PropertyMetadata((ObservableCollection<INavigationControl>)null, OnItemsChanged));

    /// <summary>
    /// Property for <see cref="Footer"/>.
    /// </summary>
    public static readonly DependencyProperty FooterProperty = DependencyProperty.Register(nameof(Footer),
        typeof(ObservableCollection<INavigationControl>), typeof(NavigationBase),
        new PropertyMetadata((ObservableCollection<INavigationControl>)null, OnFooterChanged));

    /// <summary>
    /// Property for <see cref="Frame"/>.
    /// </summary>
    public static readonly DependencyProperty FrameProperty = DependencyProperty.Register(nameof(Frame),
        typeof(Frame), typeof(NavigationBase),
        new PropertyMetadata((Frame)null, OnFrameChanged));

    /// <summary>
    /// Property for <see cref="TransitionDuration"/>.
    /// </summary>
    public static readonly DependencyProperty TransitionDurationProperty = DependencyProperty.Register(nameof(TransitionDuration),
        typeof(int), typeof(NavigationBase),
        new PropertyMetadata(300));

    /// <summary>
    /// Property for <see cref="TransitionType"/>.
    /// </summary>
    public static readonly DependencyProperty TransitionTypeProperty = DependencyProperty.Register(nameof(TransitionType),
        typeof(Services.TransitionType), typeof(NavigationBase),
        new PropertyMetadata(Services.TransitionType.FadeInWithSlide));

    /// <summary>
    /// Property for <see cref="SelectedPageIndex"/>.
    /// </summary>
    public static readonly DependencyProperty SelectedPageIndexProperty = DependencyProperty.Register(
        nameof(SelectedPageIndex),
        typeof(int), typeof(NavigationBase),
        new PropertyMetadata(0));

    /// <summary>
    /// Attached property for <see cref="INavigationItem"/>'s to get its parent.
    /// </summary>
    internal static readonly DependencyProperty NavigationParentProperty = DependencyProperty.RegisterAttached(
        nameof(NavigationParent), typeof(INavigation), typeof(NavigationBase),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));

    /// <inheritdoc/>
    public ObservableCollection<INavigationControl> Items
    {
        get => GetValue(ItemsProperty) as ObservableCollection<INavigationControl>;
        set => SetValue(ItemsProperty, value);
    }

    /// <inheritdoc/>
    public ObservableCollection<INavigationControl> Footer
    {
        get => GetValue(FooterProperty) as ObservableCollection<INavigationControl>;
        set => SetValue(FooterProperty, value);
    }

    /// <inheritdoc/>
    public int TransitionDuration
    {
        get => (int)GetValue(TransitionDurationProperty);
        set => SetValue(TransitionDurationProperty, value);
    }

    /// <inheritdoc/>
    public Services.TransitionType TransitionType
    {
        get => (Services.TransitionType)GetValue(TransitionTypeProperty);
        set => SetValue(TransitionTypeProperty, value);
    }

    /// <inheritdoc/>
    public Frame Frame
    {
        get => GetValue(FrameProperty) as Frame;
        set => SetValue(FrameProperty, value);
    }

    /// <inheritdoc/>
    public int SelectedPageIndex
    {
        get => (int)GetValue(SelectedPageIndexProperty);
        set => SetValue(SelectedPageIndexProperty, value);
    }

    internal INavigation NavigationParent
    {
        get => (INavigation)GetValue(NavigationParentProperty);
        set => SetValue(NavigationParentProperty, value);
    }

    #region Events

    /// <summary>
    /// Event triggered when <see cref="NavigationBase"/> navigate to page.
    /// </summary>
    public static readonly RoutedEvent NavigatedEvent = EventManager.RegisterRoutedEvent(nameof(Navigated),
        RoutingStrategy.Bubble, typeof(RoutedNavigationEvent), typeof(NavigationBase));

    /// <inheritdoc/>
    public event RoutedNavigationEvent Navigated
    {
        add => AddHandler(NavigatedEvent, value);
        remove => RemoveHandler(NavigatedEvent, value);
    }

    /// <summary>
    /// Event triggered when <see cref="NavigationBase"/> navigate to the next page.
    /// </summary>
    public static readonly RoutedEvent NavigatedForwardEvent =
        EventManager.RegisterRoutedEvent(nameof(NavigatedForward), RoutingStrategy.Bubble,
            typeof(RoutedNavigationEvent), typeof(NavigationBase));

    /// <inheritdoc/>
    public event RoutedNavigationEvent NavigatedForward
    {
        add => AddHandler(NavigatedForwardEvent, value);
        remove => RemoveHandler(NavigatedForwardEvent, value);
    }

    /// <summary>
    /// Event triggered when <see cref="NavigationBase"/> navigate to the previous page.
    /// </summary>
    public static readonly RoutedEvent NavigatedBackwardEvent =
        EventManager.RegisterRoutedEvent(nameof(NavigatedBackward), RoutingStrategy.Bubble,
            typeof(RoutedNavigationEvent), typeof(NavigationBase));

    /// <inheritdoc/>
    public event RoutedNavigationEvent NavigatedBackward
    {
        add => AddHandler(NavigatedBackwardEvent, value);
        remove => RemoveHandler(NavigatedBackwardEvent, value);
    }

    #endregion

    /// <summary>
    /// Navigation history containing pages tags.
    /// </summary>
    public List<string> History { get; internal set; }

    /// <inheritdoc/>
    public INavigationItem Current { get; internal set; }

    /// <inheritdoc/>
    public int PreviousPageIndex { get; internal set; }

    static NavigationBase()
    {
        KeyboardNavigation.DirectionalNavigationProperty.OverrideMetadata(
            typeof(NavigationBase),
            new FrameworkPropertyMetadata(KeyboardNavigationMode.Contained));

        KeyboardNavigation.TabNavigationProperty.OverrideMetadata(
            typeof(NavigationBase),
            new FrameworkPropertyMetadata(KeyboardNavigationMode.Once));
    }

    /// <summary>
    /// Prepares base navigation properties.
    /// </summary>
    protected NavigationBase()
    {
        _eventIdentifier = new EventIdentifier();
        _navigationServiceItems = new Dictionary<int, NavigationServiceItem>();

        InitializeBase();
    }

    /// <summary>
    /// Initializes internal properties.
    /// </summary>
    private void InitializeBase()
    {
        // Let the NavigationItem children be able to get me.
        NavigationParent = this;

        PreviousPageIndex = 0;
        Current = (INavigationItem)null;
        History = new List<string>();

        Items ??= new ObservableCollection<INavigationControl>();
        Footer ??= new ObservableCollection<INavigationControl>();

        Loaded += OnLoaded;
    }

    /// <inheritdoc/>
    public bool Navigate(string pageTag)
    {
        return Navigate(pageTag, null);
    }

    /// <inheritdoc/>
    public bool Navigate(string pageTag, object dataContext)
    {
        var selectedItem = _navigationServiceItems
            .Where(item => item.Value.Tag == pageTag)
            .Select(item => item.Key)
            .FirstOrDefault();

        if (selectedItem < 0)
            return false;

        return Navigate(selectedItem, dataContext);
    }


    /// <inheritdoc/>
    public bool Navigate(int pageId)
    {
        return Navigate(pageId, null);
    }

    /// <inheritdoc/>
    public bool Navigate(int pageId, object dataContext)
    {
        if (pageId < 0)
            return false;

        if (pageId > _navigationServiceItems.Count)
            return false;

        if (!_navigationServiceItems.TryGetValue(pageId, out var currentNavigationServiceItem))
            return false;

        // This item is invalid OR used as button
        if (currentNavigationServiceItem.Source == null && currentNavigationServiceItem.Type == null)
            return false;

        if (SelectedPageIndex == pageId && _firstPagePresented)
            return false;

        _firstPagePresented = true;

        CurrentlyNavigatedServiceItem = pageId;

        PreviousPageIndex = SelectedPageIndex;
        SelectedPageIndex = pageId;

        NavigateInstance(ref currentNavigationServiceItem, dataContext);

        History.Add(currentNavigationServiceItem.Tag);

        SetCurrentPage(currentNavigationServiceItem.Tag);

        OnNavigated();

        if (PreviousPageIndex > SelectedPageIndex)
            OnNavigatedForward();
        else
            OnNavigatedBackward();

        return false;
    }

    /// <inheritdoc/>
    public bool NavigateExternal(object frameworkElement)
    {
        if (frameworkElement is not FrameworkElement)
            throw new InvalidOperationException($"Only an object inherited from the {typeof(FrameworkElement)} class can be loaded into the navigation Frame.");

        if (Frame == null)
            return false;

        CurrentlyNavigatedServiceItem = -1;
        PreviousPageIndex = SelectedPageIndex;
        SelectedPageIndex = -1;

        SetCurrentPage("_");

        Current = (INavigationItem)null;

        Frame.Navigate(frameworkElement);

        return true;
    }

    /// <inheritdoc/>
    public bool NavigateExternal(Uri absolutePageUri)
    {
        if (!absolutePageUri.IsAbsoluteUri)
            throw new InvalidOperationException($"The Uri to the element must be absolute.");

        if (Frame == null)
            return false;

        CurrentlyNavigatedServiceItem = -1;
        PreviousPageIndex = SelectedPageIndex;
        SelectedPageIndex = -1;

        SetCurrentPage("_");

        Current = (INavigationItem)null;

        Frame.Navigate(absolutePageUri);

        return true;
    }

    /// <inheritdoc/>
    public void SetCurrentContext(object dataContext)
    {
        if (Frame?.Content is not FrameworkElement element)
            return;

        element.DataContext = dataContext;
    }

    /// <inheritdoc/>
    public void Flush()
    {
        Items.Clear();
        Footer.Clear();

        Current = (INavigationItem)null;
    }

    /// <inheritdoc/>
    public void ClearCache()
    {
        foreach (var singleServiceItem in _navigationServiceItems)
            singleServiceItem.Value.Instance = null;
    }

    /// <summary>
    /// Tries to navigate to the selected page and create a cache entry.
    /// </summary>
    private void NavigateInstance(ref NavigationServiceItem navigationServiceItem, object dataContext)
    {
        CurrentActionIdentifier = _eventIdentifier.GetNext();

        if (!navigationServiceItem.Cache)
        {
            if (navigationServiceItem.Type != null)
            {
                Frame.Navigate(CreatePageInstance(navigationServiceItem.Type, dataContext));
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"DEBUG | {navigationServiceItem.Tag} loaded by TYPE, WITHOUT CACHE. CACHE: {navigationServiceItem.Cache}");
#endif
                return;
            }

            if (navigationServiceItem.Source != null)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"DEBUG | {navigationServiceItem.Tag} loaded by SOURCE, WITHOUT CACHE. CACHE: {navigationServiceItem.Cache}");
#endif
                Frame.Navigate(navigationServiceItem.Source);

                return;
            }

            throw new InvalidOperationException(
                $"{typeof(INavigationItem)} when navigated, should have TYPE of SOURCE");
        }

        if (navigationServiceItem.Instance != null)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"DEBUG | {navigationServiceItem.Tag} loaded by INSTANCE, WITH CACHE. CACHE: {navigationServiceItem.Cache}");
#endif
            Frame.Navigate(navigationServiceItem.Instance);

            return;
        }

        if (navigationServiceItem.Type != null)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"DEBUG | {navigationServiceItem.Tag} loaded by TYPE, WITHOUT CACHE. CACHE: {navigationServiceItem.Cache}");
#endif
            navigationServiceItem.Instance = CreatePageInstance(navigationServiceItem.Type, dataContext);

            Frame.Navigate(navigationServiceItem.Instance);

#if DEBUG
            System.Diagnostics.Debug.WriteLine($"DEBUG | Cache for {navigationServiceItem.Tag} saved.");
#endif

            return;
        }

        if (navigationServiceItem.Source == null)
            throw new InvalidOperationException(
                $"{typeof(INavigationItem)} when navigated, should have TYPE of SOURCE");

#if DEBUG
        System.Diagnostics.Debug.WriteLine($"DEBUG | {navigationServiceItem.Tag} loaded by SOURCE, WITHOUT CACHE. CACHE: {navigationServiceItem.Cache}");
#endif

        Frame.Navigate(navigationServiceItem.Source);
    }

    /// <summary>
    /// Tries to create an instance from the selected page type.
    /// </summary>
    private FrameworkElement CreatePageInstance(Type pageType, object dataContext)
    {
        if (!typeof(FrameworkElement).IsAssignableFrom(pageType))
            throw new InvalidCastException(
                $"PageType of the ${typeof(INavigationItem)} must be derived from {typeof(FrameworkElement)}");

        if (Designer.IsInDesignMode)
            Frame.Navigate(new Page { Content = new TextBlock { Text = "Preview" } });

        var instance = Activator.CreateInstance(pageType);

        if (dataContext != null && instance is FrameworkElement)
            ((FrameworkElement)instance).DataContext = dataContext;

        return instance as FrameworkElement;
    }

    /// <summary>
    /// Changes the state of all but selected item to inactive and defines the <see cref="Current"/> parameter.
    /// </summary>
    private void SetCurrentPage(string currentPageTag)
    {
        foreach (var singleNavItem in Items)
        {
            if (singleNavItem is not INavigationItem navigationItem)
                continue;

            if (!navigationItem.PageTag.Equals(currentPageTag))
            {
                navigationItem.IsActive = false;

                continue;
            }

            navigationItem.IsActive = true;
            Current = navigationItem;
        }

        foreach (var singleNavItem in Footer)
        {
            if (singleNavItem is not INavigationItem navigationItem)
                continue;

            if (!navigationItem.PageTag.Equals(currentPageTag))
            {
                navigationItem.IsActive = false;

                continue;
            }

            navigationItem.IsActive = true;
            Current = navigationItem;
        }
    }

    /// <summary>
    /// This virtual method is called when <see cref="INavigation"/> is loaded.
    /// </summary>
    protected virtual void OnLoaded(object sender, RoutedEventArgs e)
    {
        RebuildServiceItems();

        if (Frame != null && SelectedPageIndex > -1)
            Navigate(SelectedPageIndex);
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        // We handle Left/Up/Right/Down keys for keyboard navigation only,
        // so no modifiers are needed.
        if (Keyboard.Modifiers is not ModifierKeys.None)
            return;

        // For most cases, this method do nothing because it does not receive focus by default.
        // But if someone set focus to it, the key handling can move the focus to its navigation children.
        switch (e.Key)
        {
            // We use Direction Left/Up/Right/Down instead of Previous/Next to make sure
            // that the KeyboardNavigation.DirectionalNavigation property works correctly.
            case Key.Left:
                MoveFocus(this, FocusNavigationDirection.Left);
                e.Handled = true;
                break;

            case Key.Up:
                MoveFocus(this, FocusNavigationDirection.Up);
                e.Handled = true;
                break;

            case Key.Right:
                MoveFocus(this, FocusNavigationDirection.Right);
                e.Handled = true;
                break;

            case Key.Down:
                MoveFocus(this, FocusNavigationDirection.Down);
                e.Handled = true;
                break;
        }

        if (!e.Handled)
            base.OnKeyDown(e);

        static void MoveFocus(FrameworkElement element, FocusNavigationDirection direction)
        {
            var request = new TraversalRequest(direction);
            element.MoveFocus(request);
        }
    }

    /// <summary>
    /// This virtual method is called during any navigation and it raises the <see cref="Navigated"/> <see langword="event"/>.
    /// </summary>
    protected virtual void OnNavigated()
    {
        var newEvent = new RoutedNavigationEventArgs(NavigatedEvent, this, Current);
        RaiseEvent(newEvent);
    }

    /// <summary>
    /// This virtual method is called during forward navigation and it raises the <see cref="NavigatedForward"/> <see langword="event"/>.
    /// </summary>
    protected virtual void OnNavigatedForward()
    {
        var newEvent = new RoutedNavigationEventArgs(NavigatedForwardEvent, this, Current);
        RaiseEvent(newEvent);
    }

    /// <summary>
    /// This virtual method is called during backward navigation and it raises the <see cref="NavigatedBackward"/> <see langword="event"/>.
    /// </summary>
    protected virtual void OnNavigatedBackward()
    {
        var newEvent = new RoutedNavigationEventArgs(NavigatedBackwardEvent, this, Current);
        RaiseEvent(newEvent);
    }

    /// <summary>
    /// This virtual method is called when one of the navigation items is clicked.
    /// </summary>
    protected virtual void OnNavigationItemClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not INavigationItem navigationItem)
            return;

        if (navigationItem.AbsolutePageSource == null && navigationItem.PageType == null)
            return;

        Navigate(navigationItem.PageTag);
    }

    /// <summary>
    /// This virtual method is called when something is added, deleted or changed in <see cref="Items"/> or <see cref="Footer"/>.
    /// </summary>
    protected virtual void OnNavigationCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (IsLoaded)
            RebuildServiceItems();

        if (e.NewItems != null)
            foreach (var addedItem in e.NewItems)
                if (addedItem is INavigationItem)
                    ((INavigationItem)addedItem).Click += OnNavigationItemClicked;

        if (e.OldItems == null)
            return;

        foreach (var deletedItem in e.OldItems)
            ((INavigationItem)deletedItem).Click -= OnNavigationItemClicked;
    }

    /// <summary>
    /// Triggered when <see cref="ItemsProperty"/> is changed.
    /// </summary>
    private static void OnItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not NavigationBase navigationBase)
            return;

        if (e.NewValue is not ObservableCollection<INavigationControl> itemsCollection)
            return;

        itemsCollection.CollectionChanged += navigationBase.OnNavigationCollectionChanged;
    }

    /// <summary>
    /// Triggered when <see cref="FooterProperty"/> is changed.
    /// </summary>
    private static void OnFooterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not NavigationBase navigationBase)
            return;

        if (e.NewValue is not ObservableCollection<INavigationControl> itemsCollection)
            return;

        itemsCollection.CollectionChanged += navigationBase.OnNavigationCollectionChanged;
    }

    /// <summary>
    /// This virtual method is called when one of the navigation items is clicked.
    /// </summary>
    protected virtual void OnFrameChanged(Frame frame)
    {
        frame.NavigationUIVisibility = NavigationUIVisibility.Hidden;

        frame.Navigating += OnFrameNavigating;
        frame.Navigated += OnFrameNavigated;
    }

    /// <summary>
    /// Triggered when <see cref="FrameProperty"/> is changed.
    /// </summary>
    private static void OnFrameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not NavigationBase navigationBase || e.NewValue is not Frame frame)
            return;

        navigationBase.OnFrameChanged(frame);
    }

    /// <summary>
    /// Event triggered when the frame has already loaded the view, if the page uses the Cache, Content of the Frame should be saved.
    /// </summary>
    protected virtual void OnFrameNavigated(object sender, NavigationEventArgs e)
    {
        if (sender is not Frame frame)
            return;

        Services.TransitionService.ApplyTransition(e.Content, TransitionType, TransitionDuration);

        if (frame.CanGoBack)
            frame.RemoveBackEntry();

        if (frame.NavigationService.CanGoBack)
            frame.NavigationService?.RemoveBackEntry();

        if (frame.Content == null)
            return;

        if (frame.Content is INavigable)
            ((INavigable)frame.Content).OnNavigationRequest(this);

        if (!_eventIdentifier.IsEqual(CurrentActionIdentifier))
            return;

        if (!_navigationServiceItems.ContainsKey(CurrentlyNavigatedServiceItem))
            return;

        if (!_navigationServiceItems[CurrentlyNavigatedServiceItem].Cache)
            return;

        if (_navigationServiceItems[CurrentlyNavigatedServiceItem].Instance != null)
            return;

        _navigationServiceItems[CurrentlyNavigatedServiceItem].Instance = frame.Content;

#if DEBUG
        System.Diagnostics.Debug.WriteLine($"DEBUG | Cache for {_navigationServiceItems[CurrentlyNavigatedServiceItem].Tag} saved.");
#endif
    }

    /// <summary>
    /// Event fired when Frame received a request to navigate.
    /// </summary>
    protected virtual void OnFrameNavigating(object sender, NavigatingCancelEventArgs e)
    {
        if (sender is not Frame frame)
            return;

        switch (e.NavigationMode)
        {
            case NavigationMode.Back:
                e.Cancel = true;

                if (CurrentlyNavigatedServiceItem > 0)
                    Navigate(CurrentlyNavigatedServiceItem - 1);
                break;

            case NavigationMode.Forward:
                e.Cancel = true;

                if (CurrentlyNavigatedServiceItem < _navigationServiceItems.Count)
                    Navigate(CurrentlyNavigatedServiceItem + 1);
                break;
        }
    }

    /// <summary>
    /// Gets the <see cref="Navigation"/> parent view for its <see cref="NavigationItem"/> children.
    /// </summary>
    /// <param name="navigationItem"></param>
    /// <returns></returns>
    internal static NavigationBase? GetNavigationParent<T>(T navigationItem)
        where T : DependencyObject, INavigationItem
    {
        return (NavigationBase?)navigationItem.GetValue(NavigationParentProperty);
    }

    /// <summary>
    /// Creates a mirror list of navigation items for internal use in the navigation service.
    /// </summary>
    private void RebuildServiceItems()
    {
        _navigationServiceItems.Clear();

        var indexShift = 0;

        foreach (var singleItem in Items)
        {
            if (singleItem is not INavigationItem navigationItem)
                continue;

            _navigationServiceItems.Add(indexShift++, NavigationServiceItem.Get(navigationItem));
        }

        foreach (var singleItem in Footer)
        {
            if (singleItem is not INavigationItem navigationItem)
                continue;

            _navigationServiceItems.Add(indexShift++, NavigationServiceItem.Get(navigationItem));
        }
    }
}
