﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Phone.Controls;
using System.Windows.Navigation;
using System.Windows.Controls.Primitives;
using WebHelpers;
using BookInfo;
using System.Collections;

namespace DouMi
{
    public partial class BookSearchResultPage : PhoneApplicationPage
    {
        BookSearchResultViewModel bookSearchResultViewModel = null;
        string SearchKey = "";

        private ScrollViewer sv = null;
        private bool alreadyHookedScrollEvents = false;
        private bool isSearchingData = false;
        private bool needLoadingMoreData = false;
        private bool isNoSearchResult = false;

        public BookSearchResultPage()
        {
            bookSearchResultViewModel = new BookSearchResultViewModel();
            InitializeComponent();
            this.Loaded += new RoutedEventHandler(BookSearchResultPage_Loaded);
        }

        private void BookSearchResultPage_Loaded(object sender, RoutedEventArgs e)
        {
            this.DataContext = bookSearchResultViewModel;
            initialScrollView();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            string key = "";
            if (NavigationContext.QueryString.TryGetValue("key", out key) && key != "" && bookSearchResultViewModel.IsItemsEmpty())
            {
                SearchKey = key;
                string bookSearchUrl = BookUrl.Instance.ConstructBookSearchUrl(SearchKey, 1, 10);
                SearchMoreBook(bookSearchUrl);
                //ApplicationTitle.Text = "豆米 @ 关键字\"" + key + "\"";
                PageTitle.Text = "\"" + key + "\"";
            }
            base.OnNavigatedTo(e);
        }

        private void BookSearchResultListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BookSearchResultListBox.SelectedIndex == -1)
                return;

            NavigationService.Navigate(new Uri("/BookDetailPanoramaPage.xaml?isbn=" + bookSearchResultViewModel.Items[BookSearchResultListBox.SelectedIndex].Isbn +
                "&title=" + bookSearchResultViewModel.Items[BookSearchResultListBox.SelectedIndex].Title, 
                UriKind.Relative));
            BookSearchResultListBox.SelectedIndex = -1;
        }

        private void SearchBook(string url)
        {
            performanceProgressBar.IsIndeterminate = true;
            WebHelper.Instance.SearchBook(url,
                    (bookList) =>
                    {
                        Dispatcher.BeginInvoke(() =>
                        {
                            performanceProgressBar.IsIndeterminate = false;

                            if (bookList.Count == 0)
                            {
                                isNoSearchResult = true;
                                noSearchResult.Visibility = System.Windows.Visibility.Visible;
                            }
                            else
                            {
                                bookSearchResultViewModel.LoadBookSearchResults(bookList);
                            }
                        });
                    },
                    () =>
                    {
                        Dispatcher.BeginInvoke(() =>
                        {
                            performanceProgressBar.IsIndeterminate = false;
                        });
                    },
                    (percentage) =>
                    {
                    }
            );
        }

        private void SearchMoreBook(string url)
        {
            isSearchingData = true;
            performanceProgressBar.IsIndeterminate = true;

            WebHelper.Instance.SearchBook(url,
                    (bookList) =>
                    {
                        Dispatcher.BeginInvoke(() =>
                        {
                            if (bookList.Count == 0)
                            {
                                isNoSearchResult = true;
                                if (bookSearchResultViewModel.IsItemsEmpty())
                                {
                                    noSearchResult.Visibility = System.Windows.Visibility.Visible;
                                }
                            }
                            else
                            {
                                bookSearchResultViewModel.AppendBookSearchResults(bookList);
                            }
                            performanceProgressBar.IsIndeterminate = false;
                        });
                        isSearchingData = false;
                    },
                    () =>
                    {
                        Dispatcher.BeginInvoke(() =>
                        {
                            performanceProgressBar.IsIndeterminate = false;
                        });
                        isSearchingData = false;
                    },
                    (percentage) =>
                    {
                    }
            );
        }

        private void initialScrollView()
        {
            if (alreadyHookedScrollEvents)
                return;
            alreadyHookedScrollEvents = true;
            BookSearchResultListBox.AddHandler(ListBox.ManipulationCompletedEvent, (EventHandler<ManipulationCompletedEventArgs>)LB_ManipulationCompleted, true);
            BookSearchResultListBox.AddHandler(ListBox.ManipulationStartedEvent, (EventHandler<ManipulationStartedEventArgs>)LB_ManipulationStarted, true);
            sv = (ScrollViewer)FindElementRecursive(BookSearchResultListBox, typeof(ScrollViewer));

            if (sv != null)
            {
                // Visual States are always on the first child of the control template 
                FrameworkElement element = VisualTreeHelper.GetChild(sv, 0) as FrameworkElement;
                if (element != null)
                {
                    VisualStateGroup vgroup = FindVisualState(element, "VerticalCompression");
                    if (vgroup != null)
                    {
                        vgroup.CurrentStateChanging += new EventHandler<VisualStateChangedEventArgs>(vgroup_CurrentStateChanging);
                    }
                }
            }
        }

        private bool NeedSearchMore()
        {
            return needLoadingMoreData && !isSearchingData && !isNoSearchResult;
        }

        private void LB_ManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
        {
            if (NeedSearchMore())
            {
                needLoadingMoreData = false;
                //Dispatcher.BeginInvoke(() =>
                //{
                    int start = bookSearchResultViewModel.Items.Count + 1;
                    int results = 10;
                    string bookSearchUrl = BookUrl.Instance.ConstructBookSearchUrl(SearchKey, start, results);
                    SearchMoreBook(bookSearchUrl);
                //});
            }
        }

        private void LB_ManipulationStarted(object sender, ManipulationStartedEventArgs e)
        {
            needLoadingMoreData = false;
        }

        private UIElement FindElementRecursive(FrameworkElement parent, Type targetType)
        {
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            UIElement returnElement = null;
            if (childCount > 0)
            {
                for (int i = 0; i < childCount; i++)
                {
                    Object element = VisualTreeHelper.GetChild(parent, i);
                    if (element.GetType() == targetType)
                    {
                        return element as UIElement;
                    }
                    else
                    {
                        returnElement = FindElementRecursive(VisualTreeHelper.GetChild(parent, i) as FrameworkElement, targetType);
                    }
                }
            }
            return returnElement;
        }
        private VisualStateGroup FindVisualState(FrameworkElement element, string name)
        {
            if (element == null)
                return null;

            IList groups = VisualStateManager.GetVisualStateGroups(element);
            foreach (VisualStateGroup group in groups)
                if (group.Name == name)
                    return group;

            return null;
        }

        private void vgroup_CurrentStateChanging(object sender, VisualStateChangedEventArgs e)
        {
            if (e.NewState.Name == "CompressionBottom")
            {
                needLoadingMoreData = true;
            }
            if (e.NewState.Name == "NoVerticalCompression" || e.NewState.Name == "CompressionTop")
            {
            }
        }
    }
}