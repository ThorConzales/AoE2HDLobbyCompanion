﻿using Commons.Constants;
using Commons.Models;
using Process.NET;
using Process.NET.Memory;
using Process.NET.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using WPFTest;

namespace Overlay {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        private const int GwlExstyle = -20;
        private const int WsExTransparent = 0x00000020;
        private DispatcherTimer _visibilityTimer;
        private DispatcherTimer _processTimer;
        private DispatcherTimer _listUpdateTimer;
        private ProcessSharp _processSharp;
        private IWindow _processWindow;
        private bool _hidden = true;
        private Configuration _config;

        public MainWindow() {
            InitializeComponent();
            StartVisibilityTimer();
            StartProcessTimer();
        }

        public void UpdateConfiguration(Configuration configuration, bool sessionRunning) {
            try {
                if (configuration != null) {
                    _config = configuration;
                    if (!_config.ShowOverlay) {
                        Hide();
                    } else if (sessionRunning) {
                        Show();
                    }
                    if (_config.ShowOverlay && _config.ShowOverlayWhenFocused && (_processWindow == null || !_processWindow.IsActivated)) {
                        Hide();
                    }
                }
            } catch { }
        }

        public void UpdatePlayers(List<Player> players, bool dm) {
            try {
                if (players == null) {
                    players = new List<Player>();
                }
                int index = 1;
                foreach (var player in players) {
                    Application.Current.Dispatcher.Invoke(() => {
                        try {
                            var totalGamesLabel = ((Label)GridContainer.FindName("Label" + index + "PlayerTotalGames"));
                            var dropsLabel = ((Label)GridContainer.FindName("Label" + index + "PlayerDrops"));
                            var winsLabel = ((Label)GridContainer.FindName("Label" + index + "PlayerWins"));
                            var locationLabel = ((Label)GridContainer.FindName("Label" + index + "PlayerLocation"));
                            var gamesLabel = ((Label)GridContainer.FindName("Label" + index + "PlayerGames"));
                            var badRepLabel = ((Label)GridContainer.FindName("Label" + index + "PlayerBadRep"));
                            var goodRepLabel = ((Label)GridContainer.FindName("Label" + index + "PlayerGoodRep"));
                            var rank = "";
                            ((Label)GridContainer.FindName("Label" + index + "PlayerName")).Content = player.Name;
                            if (player.Rank > 0) {
                                rank = player.Rank.ToString();
                            }
                            ((Label)GridContainer.FindName("Label" + index + "PlayerRank")).Content = rank;
                            if (player.GameStats != null && player.Profile != null) {
                                int totalGames = !dm ? player.GameStats.TotalGamesRM : player.GameStats.TotalGamesDM;
                                int winRatio = !dm ? player.GameStats.WinRatioRM : player.GameStats.WinRatioDM;
                                int dropRatio = !dm ? player.GameStats.DropRatioRM : player.GameStats.DropRatioDM;
                                var games = "";
                                var wins = "";
                                var drops = "";
                                if (player.Profile.ProfilePrivate) {
                                    if (player.Profile.ProfileDataFetched.HasValue) {
                                        games = "[" + totalGames.ToString() + "]";
                                        wins = "[" + winRatio + "%" + "]";
                                        drops = "[" + dropRatio + "%" + "]";
                                    } else {
                                        games = "PRIVATE ACC";
                                    }
                                } else {
                                    games = totalGames.ToString();
                                    wins = winRatio + "%";
                                    drops = dropRatio + "%";
                                }
                                totalGamesLabel.Content = games;
                                winsLabel.Content = wins;
                                dropsLabel.Content = drops;
                            } else {
                                totalGamesLabel.Content = "";
                                winsLabel.Content = "";
                                dropsLabel.Content = "";
                            }
                            if (player.Profile != null && player.Profile.Location != null) {
                                locationLabel.Content = player.Profile.Location;
                            } else {
                                locationLabel.Content = "";
                            }
                            if (player.ReputationStats != null) {
                                gamesLabel.Content = player.ReputationStats.Games;
                                badRepLabel.Content = player.ReputationStats.NegativeReputation;
                                goodRepLabel.Content = player.ReputationStats.PositiveReputation;
                            } else {
                                gamesLabel.Content = "";
                                badRepLabel.Content = "";
                                goodRepLabel.Content = "";
                            }
                            UpdateFieldColor(gamesLabel, player, PlayerFields.GAMES);
                            UpdateFieldColor(goodRepLabel, player, PlayerFields.POSITIVE_REPUTATION);
                            UpdateFieldColor(badRepLabel, player, PlayerFields.NEGATIVE_REPUTATION);
                            UpdateFieldColor(totalGamesLabel, player, PlayerFields.TOTAL_GAMES);
                            UpdateFieldColor(winsLabel, player, PlayerFields.WIN_RATIO);
                            UpdateFieldColor(dropsLabel, player, PlayerFields.DROP_RATIO);
                            index++;
                        } catch (Exception e) {
                            Console.WriteLine("Error while updating overlay slot: " + index);
                            Console.WriteLine(e.ToString());
                        }
                    });
                }
            } catch (Exception e) {
                Console.WriteLine("Error while updating overlay list");
                Console.WriteLine(e.ToString());
            }
        }

        public void ShowMessage() {
            Application.Current.Dispatcher.Invoke(() => Height = 240);
        }

        public void HideMessage() {
            Application.Current.Dispatcher.Invoke(() => Height = 200);
        }

        private void UpdateFieldColor(Label label, Player player, string fieldName) {
            switch (player.FieldColors[fieldName]) {
                case (int)PlayerFieldColors.NONE:
                    label.Background = null;
                    break;
                case (int)PlayerFieldColors.SAFE:
                    label.Background = SafeColor;
                    break;
                case (int)PlayerFieldColors.DANGER:
                    label.Background = DangerColor;
                    break;
            }
        }

        private SolidColorBrush DangerColor {
            get { return new SolidColorBrush(Color.FromArgb(50, 255, 0, 0)); }
        }

        private SolidColorBrush SafeColor {
            get { return new SolidColorBrush(Color.FromArgb(50, 0, 255, 0)); }
        }

        private void StartListUpdateTimer() {
            _listUpdateTimer = new DispatcherTimer();
            _listUpdateTimer.Tick += (sender, args) => {

            };
            _listUpdateTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);
            _listUpdateTimer.Start();
        }

        private void StartVisibilityTimer() {
            _visibilityTimer = new DispatcherTimer();
            _visibilityTimer.Tick += (sender, args) => {
                if (_config != null && _config.ShowOverlay && _config.ShowOverlayWhenFocused) {
                    if (_processWindow != null) {
                        if (_processWindow.IsActivated && _hidden) {
                            Show();
                            _hidden = false;
                        } else if (!_processWindow.IsActivated && !_hidden) {
                            Hide();
                            _hidden = true;
                        }
                    } else {
                        Hide();
                        _hidden = true;
                    }
                }
            };
            _visibilityTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            _visibilityTimer.Start();
        }

        private void StartProcessTimer() {
            _processTimer = new DispatcherTimer();
            _processTimer.Tick += (sender, args) => {
                try {
                    var process = System.Diagnostics.Process.GetProcessesByName("AoK HD").FirstOrDefault();
                    if (process != null) {
                        _processSharp = new ProcessSharp(process, MemoryType.Remote);
                        _processWindow = _processSharp.WindowFactory.MainWindow;
                    } else {
                        _processWindow = null;
                    }
                } catch {
                    _processWindow = null;
                }
            };
            _processTimer.Interval = new TimeSpan(0, 0, 1);
            _processTimer.Start();
        }

        protected override void OnSourceInitialized(EventArgs e) {
            base.OnSourceInitialized(e);
            MakeWindowTransparent();
        }

        private void MakeWindowTransparent() {
            var hwnd = new WindowInteropHelper(this).Handle;
            var extendedStyle = Native.GetWindowLongPtr(hwnd, GwlExstyle);
            Native.SetWindowLongPtr(hwnd, GwlExstyle, new IntPtr(extendedStyle.ToInt32() | WsExTransparent));
        }
    }
}