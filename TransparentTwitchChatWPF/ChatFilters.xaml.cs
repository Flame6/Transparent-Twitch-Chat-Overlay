using System;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Specialized;
using System.Diagnostics;

namespace TransparentTwitchChatWPF
{
    /// <summary>
    /// Interaction logic for ChatFilters.xaml
    /// </summary>
    public partial class ChatFilters : Window
    {
        StringCollection scAllowedUsers = new StringCollection();
        StringCollection scBlockedUsers = new StringCollection();
        StringCollection scFavoriteWords = new StringCollection();

        public ChatFilters()
        {
            InitializeComponent();

            if (App.Settings.GeneralSettings.AllowedUsersList == null)
                App.Settings.GeneralSettings.AllowedUsersList = new StringCollection();
            if (App.Settings.GeneralSettings.BlockedUsersList == null)
                App.Settings.GeneralSettings.BlockedUsersList = new StringCollection();
            if (App.Settings.GeneralSettings.FavoriteWordsList == null)
                App.Settings.GeneralSettings.FavoriteWordsList = new StringCollection();

            foreach (string s in App.Settings.GeneralSettings.AllowedUsersList)
                scAllowedUsers.Add(s);

            foreach (string s in App.Settings.GeneralSettings.BlockedUsersList)
                scBlockedUsers.Add(s);

            foreach (string s in App.Settings.GeneralSettings.FavoriteWordsList)
                scFavoriteWords.Add(s);

            refreshListBoxAllowedUsers();
            refreshListBoxBlockedUsers();
            refreshListBoxFavoriteWords();

            this.cbHighlightUsers.IsChecked = App.Settings.GeneralSettings.HighlightUsersChat;
            this.cbAllowedUsers.IsChecked = App.Settings.GeneralSettings.AllowedUsersOnlyChat;
            this.cbAllMods.IsChecked = App.Settings.GeneralSettings.FilterAllowAllMods;
            this.cbAllVIPs.IsChecked = App.Settings.GeneralSettings.FilterAllowAllVIPs;
            this.cbBlockBotActivity.IsChecked = App.Settings.GeneralSettings.BlockBotActivity;
            this.cbHighlightFavoriteWords.IsChecked = App.Settings.GeneralSettings.HighlightFavoriteWords;
            this.colorPicker.SelectedColor = App.Settings.GeneralSettings.ChatHighlightColor;
            this.colorPickerMods.SelectedColor = App.Settings.GeneralSettings.ChatHighlightModsColor;
            this.colorPickerVIPs.SelectedColor = App.Settings.GeneralSettings.ChatHighlightVIPsColor;
            this.colorPickerWords.SelectedColor = App.Settings.GeneralSettings.ChatHighlightWordsColor;
        }

        private void OnClick_RemoveAllowedUsername(object sender, RoutedEventArgs e)
        {
            if (this.lvAllowedUsernames.SelectedIndex >= 0)
            {
                this.scAllowedUsers.Remove(this.lvAllowedUsernames.SelectedItem as string);
                refreshListBoxAllowedUsers();
            }
        }

        private void OnClick_AddAllowedUsername(object sender, RoutedEventArgs e)
        {
            Input_Username inputDialog = new Input_Username();
            if (inputDialog.ShowDialog() == true)
            {
                this.scAllowedUsers.Add(inputDialog.Username);
                refreshListBoxAllowedUsers();
            }
        }

        private void OnClick_RemoveBlockedUsername(object sender, RoutedEventArgs e)
        {
            if (this.lvBlockedUsernames.SelectedIndex >= 0)
            {
                this.scBlockedUsers.Remove(this.lvBlockedUsernames.SelectedItem as string);
                refreshListBoxBlockedUsers();
            }
        }

        private void OnClick_AddBlockedUsername(object sender, RoutedEventArgs e)
        {
            Input_Username inputDialog = new Input_Username();
            if (inputDialog.ShowDialog() == true)
            {
                this.scBlockedUsers.Add(inputDialog.Username);
                refreshListBoxBlockedUsers();
            }
        }

        private void OnClick_AddFavoriteWord(object sender, RoutedEventArgs e)
        {
            Input inputDialog = new Input();
            inputDialog.Title = "Favorite Word";
            if (inputDialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(inputDialog.Channel))
            {
                this.scFavoriteWords.Add(inputDialog.Channel.Trim());
                refreshListBoxFavoriteWords();
            }
        }

        private void OnClick_RemoveFavoriteWord(object sender, RoutedEventArgs e)
        {
            if (this.lvFavoriteWords.SelectedIndex >= 0)
            {
                this.scFavoriteWords.Remove(this.lvFavoriteWords.SelectedItem as string);
                refreshListBoxFavoriteWords();
            }
        }

        private void refreshListBoxAllowedUsers()
        {
            this.lvAllowedUsernames.Focus();
            this.lvAllowedUsernames.UnselectAll();
            this.lvAllowedUsernames.Items.Clear();

            foreach (string s in this.scAllowedUsers)
                this.lvAllowedUsernames.Items.Add(s);
            
            this.lvAllowedUsernames.UnselectAll();
            this.lvAllowedUsernames.Focus();
        }

        private void refreshListBoxBlockedUsers()
        {
            this.lvBlockedUsernames.Focus();
            this.lvBlockedUsernames.UnselectAll();
            this.lvBlockedUsernames.Items.Clear();

            foreach (string s in this.scBlockedUsers)
                this.lvBlockedUsernames.Items.Add(s);

            this.lvBlockedUsernames.UnselectAll();
            this.lvBlockedUsernames.Focus();
        }

        private void refreshListBoxFavoriteWords()
        {
            this.lvFavoriteWords.Focus();
            this.lvFavoriteWords.UnselectAll();
            this.lvFavoriteWords.Items.Clear();

            foreach (string s in this.scFavoriteWords)
                this.lvFavoriteWords.Items.Add(s);

            this.lvFavoriteWords.UnselectAll();
            this.lvFavoriteWords.Focus();
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            App.Settings.GeneralSettings.HighlightUsersChat = this.cbHighlightUsers.IsChecked ?? false;
            App.Settings.GeneralSettings.AllowedUsersOnlyChat = this.cbAllowedUsers.IsChecked ?? false;
            App.Settings.GeneralSettings.FilterAllowAllMods = this.cbAllMods.IsChecked ?? false;
            App.Settings.GeneralSettings.FilterAllowAllVIPs = this.cbAllVIPs.IsChecked ?? false;
            App.Settings.GeneralSettings.AllowedUsersList = scAllowedUsers;
            App.Settings.GeneralSettings.BlockedUsersList = scBlockedUsers;
            App.Settings.GeneralSettings.BlockBotActivity = this.cbBlockBotActivity.IsChecked ?? false;
            App.Settings.GeneralSettings.HighlightFavoriteWords = this.cbHighlightFavoriteWords.IsChecked ?? false;
            App.Settings.GeneralSettings.FavoriteWordsList = scFavoriteWords;
            App.Settings.GeneralSettings.ChatHighlightColor = this.colorPicker.SelectedColor ?? App.Settings.GeneralSettings.ChatHighlightColor;
            App.Settings.GeneralSettings.ChatHighlightModsColor = this.colorPickerMods.SelectedColor ?? App.Settings.GeneralSettings.ChatHighlightModsColor;
            App.Settings.GeneralSettings.ChatHighlightVIPsColor = this.colorPickerVIPs.SelectedColor ?? App.Settings.GeneralSettings.ChatHighlightVIPsColor;
            App.Settings.GeneralSettings.ChatHighlightWordsColor = this.colorPickerWords.SelectedColor ?? App.Settings.GeneralSettings.ChatHighlightWordsColor;
            App.Settings.SyncJChatSettings();
            App.Settings.Persist();
            DialogResult = true;
        }

        private void cbAllowedUsers_Checked(object sender, RoutedEventArgs e)
        {
            cbHighlightUsers.IsChecked = false;
            cbAllMods.IsEnabled = true;
            cbAllMods.Content = "Allow all mods";
            cbAllVIPs.IsEnabled = true;
            cbAllVIPs.Content = "Allow all VIPs";
            btnAddUser.IsEnabled = true;
            btnRemoveUser.IsEnabled = true;
            lvAllowedUsernames.IsEnabled = true;
        }

        private void cbAllowedUsers_Unchecked(object sender, RoutedEventArgs e)
        {
            cbAllMods.IsEnabled = false;
            cbAllVIPs.IsEnabled = false;
            btnAddUser.IsEnabled = false;
            btnRemoveUser.IsEnabled = false;
            lvAllowedUsernames.IsEnabled = false;
        }

        private void cbHighlightUsers_Checked(object sender, RoutedEventArgs e)
        {
            cbAllowedUsers.IsChecked = false;
            cbAllMods.IsEnabled = true;
            cbAllMods.Content = "Highlight all mods";
            cbAllVIPs.IsEnabled = true;
            cbAllVIPs.Content = "Highlight all VIPs";
            btnAddUser.IsEnabled = true;
            btnRemoveUser.IsEnabled = true;
            lvAllowedUsernames.IsEnabled = true;
        }

        private void cbHighlightUsers_Unchecked(object sender, RoutedEventArgs e)
        {
            cbAllMods.IsEnabled = false;
            cbAllVIPs.IsEnabled = false;
            btnAddUser.IsEnabled = false;
            btnRemoveUser.IsEnabled = false;
            lvAllowedUsernames.IsEnabled = false;
        }

        private void lvFilters_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (this.lvFilters.SelectedIndex)
            {
                case 0:
                    filterUsernamesGrid.Visibility = Visibility.Visible;
                    filterBotsGrid.Visibility = Visibility.Hidden;
                    filterWordsGrid.Visibility = Visibility.Hidden;
                    break;
                case 1:
                    filterUsernamesGrid.Visibility = Visibility.Hidden;
                    filterBotsGrid.Visibility = Visibility.Visible;
                    filterWordsGrid.Visibility = Visibility.Hidden;
                    break;
                case 2:
                    filterUsernamesGrid.Visibility = Visibility.Hidden;
                    filterBotsGrid.Visibility = Visibility.Hidden;
                    filterWordsGrid.Visibility = Visibility.Visible;
                    break;
            }
        }
    }
}
