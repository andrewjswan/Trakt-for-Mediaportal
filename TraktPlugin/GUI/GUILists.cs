﻿using MediaPortal.GUI.Library;
using MediaPortal.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TraktAPI.DataStructures;
using TraktPlugin.Extensions;
using TraktPlugin.TraktHandlers;
using Action = MediaPortal.GUI.Library.Action;

namespace TraktPlugin.GUI
{
    public class GUILists : GUIWindow
    {
        #region Skin Controls

        [SkinControl(50)]
        protected GUIFacadeControl Facade = null;

        #endregion

        #region Enums

        enum ContextMenuItem
        {
            Like,
            Unlike,
            Create,
            Delete,
            Edit,
            Copy,
            Comments  
        }

        #endregion

        #region Constructor

        public GUILists() { }

        #endregion

        #region Private Variables

        bool StopDownload { get; set; }
        bool ReturningFromListItemsOrComments { get; set; }
        static int PreviousSelectedIndex { get; set; }
        IEnumerable<TraktListDetail> Lists { get; set; }

        #endregion

        #region Public Properties

        public static string CurrentUser { get; set; }
        public static TraktListType ListType { get; set; }

        #endregion

        #region Base Overrides

        public override int GetID
        {
            get
            {
                return (int)TraktGUIWindows.CustomLists;
            }
        }

        public override bool Init()
        {
            return Load(GUIGraphicsContext.Skin + @"\Trakt.Lists.xml");
        }

        protected override void OnPageLoad()
        {
            base.OnPageLoad();

            // Clear GUI Properties
            GUICommon.ClearListProperties();

            // Requires Login
            if (!GUICommon.CheckLogin()) return;

            // Init Properties
            InitProperties();

            // Load Lists basis type
            LoadLists();
        }

        protected override void OnPageDestroy(int new_windowId)
        {
            StopDownload = true;
            PreviousSelectedIndex = Facade.SelectedListItemIndex;
            GUICommon.ClearListProperties();

            base.OnPageDestroy(new_windowId);
        }

        protected override void OnClicked(int controlId, GUIControl control, Action.ActionType actionType)
        {
            // wait for any background action to finish
            if (GUIBackgroundTask.Instance.IsBusy) return;

            switch (controlId)
            {
                // Facade
                case (50):
                    if (actionType == Action.ActionType.ACTION_SELECT_ITEM)
                    {
                        GUIListItem selectedItem = this.Facade.SelectedListItem;
                        if (selectedItem == null) return;

                        TraktListDetail selectedList = null;
                        string username = CurrentUser;

                        if (selectedItem.TVTag is TraktListDetail)
                        {
                            selectedList = selectedItem.TVTag as TraktListDetail;
                        }
                        else if (selectedItem.TVTag is TraktListTrending)
                        {
                            var trending = selectedItem.TVTag as TraktListTrending;
                            selectedList = trending.List;
                            username = trending.List.User.Username;
                        }
                        else if (selectedItem.TVTag is TraktListPopular)
                        {
                            var popular = selectedItem.TVTag as TraktListPopular;
                            selectedList = popular.List;
                            username = popular.List.User.Username;
                        }
                        else if (selectedItem.TVTag is TraktLike)
                        {
                            var likedItem = selectedItem.TVTag as TraktLike;
                            selectedList = likedItem.List;
                            username = likedItem.List.User.Username;
                        }

                        // Load current selected list
                        GUIListItems.CurrentList = selectedList;
                        GUIListItems.CurrentUser = username;
                        ReturningFromListItemsOrComments = true;

                        GUIWindowManager.ActivateWindow((int)TraktGUIWindows.CustomListItems);
                    }
                    break;

                default:
                    break;
            }
            base.OnClicked(controlId, control, actionType);
        }

        public override void OnAction(Action action)
        {
            switch (action.wID)
            {
                case Action.ActionType.ACTION_PREVIOUS_MENU:
                    // restore current user
                    CurrentUser = TraktSettings.Username;
                    ReturningFromListItemsOrComments = false;
                    base.OnAction(action);
                    break;
                default:
                    base.OnAction(action);
                    break;
            }
        }

        protected override void OnShowContextMenu()
        {
            if (GUIBackgroundTask.Instance.IsBusy) return;

            GUIListItem selectedItem = this.Facade.SelectedListItem;
            if (selectedItem == null) return;

            TraktListDetail selectedList = null;
            string username = CurrentUser;

            if (selectedItem.TVTag is TraktListDetail)
            {
                selectedList = selectedItem.TVTag as TraktListDetail;
            }
            else if (selectedItem.TVTag is TraktListTrending)
            {
                var trending = selectedItem.TVTag as TraktListTrending;
                selectedList = trending.List;
                username = trending.List.User.Username;
            }
            else if (selectedItem.TVTag is TraktListPopular)
            {
                var popular = selectedItem.TVTag as TraktListPopular;
                selectedList = popular.List;
                username = popular.List.User.Username;
            }
            else if (selectedItem.TVTag is TraktLike)
            {
                var likedItem = selectedItem.TVTag as TraktLike;
                selectedList = likedItem.List;
                username = likedItem.List.User.Username;
            }

            var dlg = (IDialogbox)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_MENU);
            if (dlg == null) return;

            dlg.Reset();
            dlg.SetHeading(GUIUtils.PluginName());

            GUIListItem listItem = null;

            // only allow add/delete/update if viewing your own lists
            if (username == TraktSettings.Username)
            {
                listItem = new GUIListItem(Translation.CreateList);
                dlg.Add(listItem);
                listItem.ItemId = (int)ContextMenuItem.Create;

                listItem = new GUIListItem(Translation.EditList);
                dlg.Add(listItem);
                listItem.ItemId = (int)ContextMenuItem.Edit;

                listItem = new GUIListItem(Translation.DeleteList);
                dlg.Add(listItem);
                listItem.ItemId = (int)ContextMenuItem.Delete;
            }
            else
            {
                // like list
                if (!selectedList.IsLiked())
                {
                    listItem = new GUIListItem(Translation.Like);
                    dlg.Add(listItem);
                    listItem.ItemId = (int)ContextMenuItem.Like;
                }
                else
                {
                    // unLike list
                    listItem = new GUIListItem(Translation.UnLike);
                    dlg.Add(listItem);
                    listItem.ItemId = (int)ContextMenuItem.Unlike;
                }

                // copy a friends list
                listItem = new GUIListItem(Translation.CopyList);
                dlg.Add(listItem);
                listItem.ItemId = (int)ContextMenuItem.Copy;
            }

            // allow viewing comments for any type of lists
            // if comments are not allowed there will most like be no comments
            if (selectedList.AllowComments)
            {
                listItem = new GUIListItem(Translation.Comments + "...");
                dlg.Add(listItem);
                listItem.ItemId = (int)ContextMenuItem.Comments;
            }

            // Show Context Menu
            dlg.DoModal(GUIWindowManager.ActiveWindow);
            if (dlg.SelectedId < 0) return;
            
            var currentList = new TraktListDetail
            {
                Ids = selectedList.Ids,
                Name = selectedList.Name,
                Description = selectedList.Description,
                Privacy = selectedList.Privacy,
                AllowComments = selectedList.AllowComments,
                DisplayNumbers = selectedList.DisplayNumbers,
                ItemCount = selectedList.ItemCount,
                Likes = selectedList.Likes,
                UpdatedAt = selectedList.UpdatedAt
            };

            switch (dlg.SelectedId)
            {
                case ((int)ContextMenuItem.Create):
                    var list = new TraktListDetail();
                    if (TraktLists.GetListDetailsFromUser(ref list))
                    {
                        if (Lists.Any(l => l.Name == list.Name))
                        {
                            // list with that name already exists
                            GUIUtils.ShowNotifyDialog(Translation.Lists, Translation.ListNameAlreadyExists);
                            return;
                        }
                        TraktLogger.Info("Creating new list for user online. Privacy = '{0}', Name = '{1}'", list.Privacy, list.Name);
                        CreateList(list);
                    }
                    break;

                case ((int)ContextMenuItem.Delete):
                    DeleteList(selectedList);
                    break;

                case ((int)ContextMenuItem.Edit):                    
                    if (TraktLists.GetListDetailsFromUser(ref currentList))
                    {
                        TraktLogger.Info("Editing list. Name = '{0}', Id = '{1}'", currentList.Name);
                        EditList(currentList);
                    }
                    break;

                case ((int)ContextMenuItem.Copy):
                    if (TraktLists.GetListDetailsFromUser(ref currentList))
                    {
                        CopyList(selectedList, currentList);
                    }
                    break;

                case ((int)ContextMenuItem.Like):
                    GUICommon.LikeList(selectedList);
                    selectedList.Likes++;
                    PublishListProperties(selectedList);
                    break;

                case ((int)ContextMenuItem.Unlike):
                    GUICommon.UnLikeList(selectedList);
                    if (selectedList.Likes > 0)
                    {
                        // different behaviour basis the current view
                        if (ListType == TraktListType.Liked)
                        {
                            // remove liked list from cache and reload
                            TraktLists.RemovedItemFromLikedListCache(selectedList.Ids.Trakt);
                            LoadLists();
                        }
                        else
                        {
                            // update selected list properties as we have unliked it now.
                            selectedList.Likes--;
                            PublishListProperties(selectedList);
                        }
                    }
                    break;

                case ((int)ContextMenuItem.Comments):
                    ReturningFromListItemsOrComments = true;
                    TraktHelper.ShowListShouts(selectedList);
                    break;
                
                default:
                    break;
            }

            base.OnShowContextMenu();
        }

        #endregion

        #region Private Methods

        private void CopyList(TraktListDetail sourceList, TraktList newList)
        {
            var copyList = new CopyList {
                Username = CurrentUser,
                Source = sourceList,
                Destination = newList
            };
            
            var copyThread = new Thread((obj) =>
            {
                var copyParams = obj as CopyList;

                // first create new list
                TraktLogger.Info("Creating new list online. Privacy = '{0}', Name = '{1}'", copyParams.Destination.Privacy, copyParams.Destination.Name);
                var response = TraktAPI.TraktAPI.CreateCustomList(copyParams.Destination);
                if (response == null || response.Ids == null)
                {
                    TraktLogger.Error("Failed to create user list. List Name = '{0}'", copyParams.Destination.Name);
                    return;
                }
                
                // get items from other list
                var userListItems = TraktAPI.TraktAPI.GetUserListItems(copyParams.Source.User.Ids.Slug, copyParams.Source.Ids.Trakt.ToString(), "min");
                if (userListItems == null)
                {
                    TraktLogger.Error("Failed to get user list items. List Name = '{0}', ID = '{1}'", copyParams.Destination.Name, copyParams.Source.Ids.Trakt);
                    return;
                }

                // copy items to new list
                var itemsToAdd = new TraktSyncAll();
                foreach (var item in userListItems)
                {
                    var listItem = new TraktListItem();
                    listItem.Type = item.Type;
                        
                    switch (item.Type)
                    {
                        case "movie":
                            if (itemsToAdd.Movies == null)
                                itemsToAdd.Movies = new List<TraktMovie>();

                            itemsToAdd.Movies.Add(new TraktMovie { Ids = item.Movie.Ids });
                            break;

                        case "show":
                            if (itemsToAdd.Shows == null)
                                itemsToAdd.Shows = new List<TraktShow>();

                            itemsToAdd.Shows.Add(new TraktShow { Ids = item.Show.Ids });
                            break;

                        case "season":
                            if (itemsToAdd.Seasons == null)
                                itemsToAdd.Seasons = new List<TraktSeason>();

                            itemsToAdd.Seasons.Add(new TraktSeason { Ids = item.Season.Ids });
                            break;

                        case "episode":
                            if (itemsToAdd.Episodes == null)
                                itemsToAdd.Episodes = new List<TraktEpisode>();

                            itemsToAdd.Episodes.Add(new TraktEpisode { Ids = item.Episode.Ids });
                            break;

                        case "person":
                            if (itemsToAdd.People == null)
                                itemsToAdd.People = new List<TraktPerson>();

                            itemsToAdd.People.Add(new TraktPerson { Ids = item.Person.Ids });
                            break;
                    }
                }

                // add items to the list
                var ItemsAddedResponse = TraktAPI.TraktAPI.AddItemsToList("me", response.Ids.Trakt.ToString(), itemsToAdd);

                if (ItemsAddedResponse != null)
                {
                    TraktLists.ClearListCache(TraktSettings.Username);
                    TraktCache.ClearCustomListCache();

                    // updated MovingPictures categories and filters menu
                    if (TraktHelper.IsMovingPicturesAvailableAndEnabled)
                    {
                        MovingPictures.UpdateCategoriesMenu(SyncListType.CustomList);
                        MovingPictures.UpdateFiltersMenu(SyncListType.CustomList);
                    }
                }
            })
            {
                Name = "CopyList",
                IsBackground = true
            };
            copyThread.Start(copyList);
        }

        private void DeleteList(TraktListDetail list)
        {
            if (!GUIUtils.ShowYesNoDialog(Translation.Lists, Translation.ConfirmDeleteList, false))
            {
                return;
            }

            GUIBackgroundTask.Instance.ExecuteInBackgroundAndCallback(() =>
            {
                TraktLogger.Info("Deleting list from online. Name = '{0}', Id = '{1}'", list.Name, list.Ids.Trakt);
                return TraktAPI.TraktAPI.DeleteUserList("me", list.Ids.Trakt.ToString());
            },
            delegate(bool success, object result)
            {
                if (success)
                {
                    if ((result as bool?) == true)
                    {
                        // remove from MovingPictures categories and filters menu
                        if (TraktHelper.IsMovingPicturesAvailableAndEnabled)
                        {
                            // not very thread safe if we tried to delete more than one before response!
                            TraktHandlers.MovingPictures.RemoveCustomListNode(list.Name);
                        }

                        // reload with new list
                        TraktLists.ClearListCache(TraktSettings.Username);
                        LoadLists();
                    }
                    else
                    {
                        GUIUtils.ShowNotifyDialog(Translation.Lists, Translation.FailedDeleteList);
                    }
                }
            }, Translation.DeletingList, true);
        }

        private void CreateList(TraktList list)
        {
            GUIBackgroundTask.Instance.ExecuteInBackgroundAndCallback(() =>
            {
                return TraktAPI.TraktAPI.CreateCustomList(list);
            },
            delegate(bool success, object result)
            {
                if (success)
                {
                    var response = result as TraktListDetail;
                    if (response != null)
                    {
                        // add to MovingPictures categories and filters menu
                        if (TraktHelper.IsMovingPicturesAvailableAndEnabled)
                        {
                            // not very thread safe if we tried to add more than one before response!
                            TraktHandlers.MovingPictures.AddCustomListNode(list.Name);
                        }

                        // reload with new list
                        TraktLists.ClearListCache(TraktSettings.Username);
                        LoadLists();
                    }
                    else
                    {
                        GUIUtils.ShowNotifyDialog(Translation.Lists, Translation.FailedCreateList);
                    }
                }
            }, Translation.CreatingList, true);
        }

        private void EditList(TraktListDetail list) 
        {
            GUIBackgroundTask.Instance.ExecuteInBackgroundAndCallback(() =>
            {
                return TraktAPI.TraktAPI.UpdateCustomList(list);
            },
            delegate(bool success, object result)
            {
                if (success)
                {
                    var response = result as TraktListDetail;
                    if (response == null)
                    {
                        // reload with new list
                        TraktLists.ClearListCache(TraktSettings.Username);
                        LoadLists();

                        var thread = new Thread((o) =>
                        {
                            TraktCache.ClearCustomListCache();

                            // updated MovingPictures categories and filters menu
                            if (TraktHelper.IsMovingPicturesAvailableAndEnabled)
                            {
                                TraktHandlers.MovingPictures.UpdateCategoriesMenu(SyncListType.CustomList);
                                TraktHandlers.MovingPictures.UpdateFiltersMenu(SyncListType.CustomList);
                            }
                        })
                        {
                            Name = "EditList",
                            IsBackground = true
                        };
                        thread.Start();
                    }
                    else
                    {
                        GUIUtils.ShowNotifyDialog(Translation.Lists, Translation.FailedUpdateList);
                    }
                }
            }, Translation.EditingList, true);
        }

        private void LoadLists()
        {
            GUIUtils.SetProperty("#Trakt.Items", string.Empty);

            GUIBackgroundTask.Instance.ExecuteInBackgroundAndCallback(() =>
            {
                switch (ListType)
                {
                    case TraktListType.Trending:
                        return TraktLists.GetTrendingLists();
                    case TraktListType.Popular:
                        return TraktLists.GetPopularLists();
                    case TraktListType.Liked:
                        return TraktLists.GetLikedLists();
                    default:
                        return TraktLists.GetListsForUser(CurrentUser);
                }
            },
            delegate(bool success, object result)
            {
                if (success)
                {
                    switch (ListType)
                    {
                        case TraktListType.Trending:
                            SendTrendingListsToFacade(result as IEnumerable<TraktListTrending>);
                            break;
                        case TraktListType.Popular:
                            SendPopularListsToFacade(result as IEnumerable<TraktListPopular>);
                            break;
                        case TraktListType.Liked:
                            SendLikedListsToFacade(result as IEnumerable<TraktLike>);
                            break;
                        default:
                            Lists = result as IEnumerable<TraktListDetail>;
                            SendUserListsToFacade(Lists);
                            break;
                    }
                }
            }, Translation.GettingLists, true);
        }

        private void SendTrendingListsToFacade(IEnumerable<TraktListTrending> lists)
        {
            // clear facade
            GUIControl.ClearControl(GetID, Facade.GetID);

            if (lists == null)
            {
                GUIUtils.ShowNotifyDialog(Translation.Error, Translation.ErrorGeneral);
                GUIWindowManager.ShowPreviousWindow();
                return;
            }
            
            int itemId = 0;

            // Add each list
            foreach (var trending in lists)
            {
                var item = new GUIListItem(trending.List.Name.RemapHighOrderChars());

                item.Label2 = string.Format("{0} {1}", trending.List.ItemCount, trending.List.ItemCount != 1 ? Translation.Items : Translation.Item);
                item.TVTag = trending;
                item.ItemId = Int32.MaxValue - itemId;
                item.PinImage = TraktLists.GetPrivacyLevelIcon(trending.List.Privacy);
                item.IconImage = "defaultFolder.png";
                item.IconImageBig = "defaultFolderBig.png";
                item.ThumbnailImage = "defaultFolderBig.png";
                item.OnItemSelected += OnItemSelected;
                Utils.SetDefaultIcons(item);
                Facade.Add(item);
                itemId++;
            }

            // Set Facade Layout
            Facade.CurrentLayout = GUIFacadeControl.Layout.List;
            GUIControl.FocusControl(GetID, Facade.GetID);

            if (PreviousSelectedIndex >= lists.Count())
                Facade.SelectIndex(PreviousSelectedIndex - 1);
            else
                Facade.SelectIndex(PreviousSelectedIndex);

            // set facade properties
            GUIUtils.SetProperty("#itemcount", lists.Count().ToString());
            GUIUtils.SetProperty("#Trakt.Items", string.Format("{0} {1}", lists.Count().ToString(), lists.Count() > 1 ? Translation.Lists : Translation.List));
        }

        private void SendPopularListsToFacade(IEnumerable<TraktListPopular> lists)
        {
            // clear facade
            GUIControl.ClearControl(GetID, Facade.GetID);

            if (lists == null)
            {
                GUIUtils.ShowNotifyDialog(Translation.Error, Translation.ErrorGeneral);
                GUIWindowManager.ShowPreviousWindow();
                return;
            }

            int itemId = 0;

            // Add each list
            foreach (var popular in lists)
            {
                var item = new GUIListItem(popular.List.Name.RemapHighOrderChars());

                item.Label2 = string.Format("{0} {1}", popular.List.ItemCount, popular.List.ItemCount != 1 ? Translation.Items : Translation.Item);
                item.TVTag = popular;
                item.ItemId = Int32.MaxValue - itemId;
                item.PinImage = TraktLists.GetPrivacyLevelIcon(popular.List.Privacy);
                item.IconImage = "defaultFolder.png";
                item.IconImageBig = "defaultFolderBig.png";
                item.ThumbnailImage = "defaultFolderBig.png";
                item.OnItemSelected += OnItemSelected;
                Utils.SetDefaultIcons(item);
                Facade.Add(item);
                itemId++;
            }

            // Set Facade Layout
            Facade.CurrentLayout = GUIFacadeControl.Layout.List;
            GUIControl.FocusControl(GetID, Facade.GetID);

            if (PreviousSelectedIndex >= lists.Count())
                Facade.SelectIndex(PreviousSelectedIndex - 1);
            else
                Facade.SelectIndex(PreviousSelectedIndex);

            // set facade properties
            GUIUtils.SetProperty("#itemcount", lists.Count().ToString());
            GUIUtils.SetProperty("#Trakt.Items", string.Format("{0} {1}", lists.Count().ToString(), lists.Count() > 1 ? Translation.Lists : Translation.List));
        }

        private void SendLikedListsToFacade(IEnumerable<TraktLike> likedItems)
        {
            // clear facade
            GUIControl.ClearControl(GetID, Facade.GetID);

            if (likedItems == null)
            {
                GUIUtils.ShowNotifyDialog(Translation.Error, Translation.ErrorGeneral);
                GUIWindowManager.ShowPreviousWindow();
                return;
            }

            int itemId = 0;

            // Add each list
            foreach (var likedItem in likedItems)
            {
                var item = new GUIListItem(likedItem.List.Name.RemapHighOrderChars());

                item.Label2 = string.Format("{0} {1}", likedItem.List.ItemCount, likedItem.List.ItemCount != 1 ? Translation.Items : Translation.Item);
                item.TVTag = likedItem;
                item.ItemId = Int32.MaxValue - itemId;
                item.PinImage = TraktLists.GetPrivacyLevelIcon(likedItem.List.Privacy);
                item.IconImage = "defaultFolder.png";
                item.IconImageBig = "defaultFolderBig.png";
                item.ThumbnailImage = "defaultFolderBig.png";
                item.OnItemSelected += OnItemSelected;
                Utils.SetDefaultIcons(item);
                Facade.Add(item);
                itemId++;
            }

            // Set Facade Layout
            Facade.CurrentLayout = GUIFacadeControl.Layout.List;
            GUIControl.FocusControl(GetID, Facade.GetID);

            if (PreviousSelectedIndex >= likedItems.Count())
                Facade.SelectIndex(PreviousSelectedIndex - 1);
            else
                Facade.SelectIndex(PreviousSelectedIndex);

            // set facade properties
            GUIUtils.SetProperty("#itemcount", likedItems.Count().ToString());
            GUIUtils.SetProperty("#Trakt.Items", string.Format("{0} {1}", likedItems.Count().ToString(), likedItems.Count() > 1 ? Translation.Lists : Translation.List));
        }

        private void SendUserListsToFacade(IEnumerable<TraktListDetail> lists)
        {
            // clear facade
            GUIControl.ClearControl(GetID, Facade.GetID);

            if (lists == null)
            {
                GUIUtils.ShowNotifyDialog(Translation.Error, Translation.ErrorGeneral);
                GUIWindowManager.ShowPreviousWindow();
                return;
            }

            // check if the user has any lists (not the currently logged in user)
            if (ListType == TraktListType.User && lists.Count() == 0 && TraktSettings.Username != CurrentUser)
            {
                GUIUtils.ShowNotifyDialog(GUIUtils.PluginName(), string.Format(Translation.NoUserLists, CurrentUser));
                CurrentUser = TraktSettings.Username;
                GUIWindowManager.ShowPreviousWindow();
                return;
            }

            // check if the currently logged in user has any lists
            // if none, prompt to create one
            if (ListType == TraktListType.User && lists.Count() == 0)
            {
                if (!GUIUtils.ShowYesNoDialog(Translation.Lists, Translation.NoListsFound, true))
                {
                    // nothing to do, exit
                    GUIWindowManager.ShowPreviousWindow();
                    return;
                }

                var list = new TraktListDetail();
                if (TraktLists.GetListDetailsFromUser(ref list))
                {
                    TraktLogger.Info("Creating new list online. Privacy = '{0}', Name = '{1}'", list.Privacy, list.Name);
                    CreateList(list);
                }
                return;
            }

            int itemId = 0;

            // Add each list
            foreach (var list in lists)
            {
                var item = new GUIListItem(list.Name);

                item.Label2 = string.Format("{0} {1}", list.ItemCount, list.ItemCount != 1 ? Translation.Items : Translation.Item);
                item.TVTag = list;
                item.ItemId = Int32.MaxValue - itemId;
                item.PinImage = TraktLists.GetPrivacyLevelIcon(list.Privacy);
                item.IconImage = "defaultFolder.png";
                item.IconImageBig = "defaultFolderBig.png";
                item.ThumbnailImage = "defaultFolderBig.png";
                item.OnItemSelected += OnItemSelected;
                Utils.SetDefaultIcons(item);
                Facade.Add(item);
                itemId++;
            }

            // Set Facade Layout
            Facade.CurrentLayout = GUIFacadeControl.Layout.List;
            GUIControl.FocusControl(GetID, Facade.GetID);

            if (PreviousSelectedIndex >= lists.Count())
                Facade.SelectIndex(PreviousSelectedIndex - 1);
            else
                Facade.SelectIndex(PreviousSelectedIndex);

            // set facade properties
            GUIUtils.SetProperty("#itemcount", lists.Count().ToString());
            GUIUtils.SetProperty("#Trakt.Items", string.Format("{0} {1}", lists.Count().ToString(), lists.Count() > 1 ? Translation.Lists : Translation.List));
        }

        private void InitProperties()
        {
            // restore list type if returning to window
            // loading parameter will be lost if loaded list items 
            // or comments for a selected list
            TraktListType LastListType = ListType;

            // check if skin is using hyperlinkParameter
            if (!string.IsNullOrEmpty(_loadParameter))
            {
                if (_loadParameter.ToLowerInvariant() == "trending")
                    ListType = TraktListType.Trending;
                if (_loadParameter.ToLowerInvariant() == "popular")
                    ListType = TraktListType.Popular;
                if (_loadParameter.ToLowerInvariant() == "liked")
                    ListType = TraktListType.Liked;
            }
            else if (!ReturningFromListItemsOrComments)
            {
                // default to user lists
                ListType = TraktListType.User;
            }

            // if we're in a different list view since last time
            // then reset last selected index
            if (LastListType != ListType)
                PreviousSelectedIndex = 0;

            // set current user to logged in user if not set
            if (string.IsNullOrEmpty(CurrentUser))
                CurrentUser = TraktSettings.Username;

            if (ListType == TraktListType.Trending)
            {
                GUIUtils.SetProperty("#Trakt.List.LikesThisWeek", string.Empty);
                GUIUtils.SetProperty("#Trakt.List.CommentsThisWeek", string.Empty);
            }

            GUICommon.SetProperty("#Trakt.Lists.ListType", ListType.ToString());
            GUICommon.SetProperty("#Trakt.Lists.CurrentUser", CurrentUser);
        }

        private void PublishListProperties(TraktListDetail list)
        {
            if (list == null) return;
            GUICommon.SetListProperties(list);
        }

        private void OnItemSelected(GUIListItem item, GUIControl parent)
        {
            TraktListDetail list = null;
            if (item.TVTag is TraktListDetail)
            {
                list = item.TVTag as TraktListDetail;
            }
            else if (item.TVTag is TraktListTrending)
            {
                var trending = item.TVTag as TraktListTrending;
                list = trending.List;

                GUICommon.SetProperty("#Trakt.List.LikesThisWeek", trending.LikesThisWeek);
                GUICommon.SetProperty("#Trakt.List.CommentsThisWeek", trending.CommentsThisWeek);
            }
            else if (item.TVTag is TraktListPopular)
            {
                var popular = item.TVTag as TraktListPopular;
                list = popular.List;
            }
            else if (item.TVTag is TraktLike)
            {
                var likedItem = item.TVTag as TraktLike;
                list = likedItem.List;
            }
            GUICommon.SetListProperties(list);
        }

        #endregion
    }

    internal class CopyList
    {
        public string Username { get; set; }
        public TraktListDetail Source { get; set; }
        public TraktList Destination { get; set; }
    }
}