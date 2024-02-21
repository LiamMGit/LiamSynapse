using System.Collections.Generic;
using System.Linq;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using IPA.Utilities.Async;
using JetBrains.Annotations;
using Synapse.Managers;
using Synapse.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Synapse.Views
{
    [ViewDefinition("Synapse.Resources.Leaderboard.bsml")]
    internal class EventLeaderboardViewController : BSMLAutomaticViewController
    {
        private static readonly string[] _randomMotivationals =
        {
            "glhf!",
            "Good luck!",
            "Break a leg!",
            "Best of luck!",
            "Knock 'em dead!",
            "Blow them away!",
            "Knock their socks off!",
            "Leave them breathless!",
            "May the odds be in your favor!",
            "Bring it on!"
        };

        [UIComponent("vertical")]
        private readonly VerticalLayoutGroup _root = null!;

        [UIObject("leaderboard")]
        private readonly GameObject _leaderboardObject = null!;

        [UIComponent("header")]
        private readonly ImageView _header = null!;

        [UIObject("noscore")]
        private readonly GameObject _noScoreObject = null!;

        [UIComponent("motivation")]
        private readonly TextMeshProUGUI _motivational = null!;

        [UIComponent("segments")]
        private readonly TextSegmentedControl _textSegments = null!;

        [UIComponent("modal")]
        private readonly ModalView _modal = null!;

        private readonly Dictionary<int, LeaderboardScores> _leaderboardScores = new();

        private NetworkManager _networkManager = null!;
        private Config _config = null!;

        private LeaderboardTableView _leaderboardTable = null!;
        private LoadingControl _loadingControl = null!;

        private bool _dirtyTextSegments;
        private string[]? _textSegmentTexts;

        private int _maxIndex;
        private int _index;

        [UsedImplicitly]
        [UIValue("showEliminated")]
        private bool ShowEliminated
        {
            get => _config.ShowEliminated;
            set
            {
                _config.ShowEliminated = value;
                ChangeView(_index);
            }
        }

        internal void ChangeSelection(int index)
        {
            _textSegments.SelectCellWithNumber(index);
            ChangeView(index);
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);

            if (firstActivation)
            {
                _leaderboardTable = GetComponentInChildren<LeaderboardTableView>(true);
                _loadingControl = GetComponentInChildren<LoadingControl>(true);

                _header.color0 = new Color(1, 1, 1, 1);
                _header.color1 = new Color(1, 1, 1, 0);

                // for some reason rebuilding it twice fixes it???
                RectTransform rootRect = (RectTransform)_root.transform;
                LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
                LayoutRebuilder.MarkLayoutForRebuild(rootRect);

                DestroyImmediate(_textSegments.gameObject.GetComponent<HorizontalLayoutGroup>());
                VerticalLayoutGroup vertical = _textSegments.gameObject.AddComponent<VerticalLayoutGroup>();
                vertical.childControlHeight = false;
                vertical.childForceExpandHeight = false;
                _textSegments._fontSize = 4;
            }

            if (addedToHierarchy)
            {
                _leaderboardScores.Clear();
                ChangeView(_maxIndex);
            }

            _motivational.text = _randomMotivationals[Random.Range(0, _randomMotivationals.Length - 1)];
        }

        [UsedImplicitly]
        [Inject]
        private void Construct(NetworkManager networkManager, Config config)
        {
            _networkManager = networkManager;
            networkManager.LeaderboardReceived += OnLeaderboardReceived;
            networkManager.MapUpdated += OnMapUpdated;
            _config = config;
        }

        private void Refresh()
        {
            _loadingControl.ShowLoading();
            _leaderboardTable.SetScores(null, -1);
            _ = _networkManager.SendInt(_index, ServerOpcode.LeaderboardRequest);
        }

        private void OnMapUpdated(int index, Map map)
        {
            _maxIndex = index;
            _textSegmentTexts = Enumerable.Range(1, index + 1).Select(n => n.ToString()).ToArray();
            _dirtyTextSegments = true;
        }

        private void Update()
        {
            if (!_dirtyTextSegments)
            {
                return;
            }

            _dirtyTextSegments = false;
            _textSegments.SetTexts(_textSegmentTexts);
            _textSegments.SelectCellWithNumber(_index);
        }

        private void OnLeaderboardReceived(LeaderboardScores leaderboardScores)
        {
            _leaderboardScores[leaderboardScores.Index] = leaderboardScores;
            UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                if (leaderboardScores.Index != _index)
                {
                    return;
                }

                _loadingControl.Hide();
                AssignScores(leaderboardScores);
            });
        }

        private void ChangeView(int index)
        {
            _index = index;
            if (_leaderboardScores.TryGetValue(index, out LeaderboardScores scores))
            {
                AssignScores(scores);
            }
            else
            {
                Refresh();
            }
        }

        private void AssignScores(LeaderboardScores scores)
        {
            List<LeaderboardCell> cells = ShowEliminated ? scores.ElimScores : scores.Scores;
            if (cells.Count > 0)
            {
                List<LeaderboardTableView.ScoreData> data = cells.Select(n =>
                {
                    string colorText = n.Color;
                    Color? color = null;
                    if (ColorUtility.TryParseHtmlString(colorText, out Color parsedColor))
                    {
                        color = parsedColor;
                    }

                    return (LeaderboardTableView.ScoreData)new EventScoreData(
                        n.Score,
                        n.PlayerName,
                        n.Rank + 1,
                        n.FullCombo,
                        color);
                }).ToList();
                int playerScoreIndex = ShowEliminated ? scores.ElimPlayerScoreIndex : scores.PlayerScoreIndex;
                _leaderboardTable.SetScores(data, playerScoreIndex);
                _noScoreObject.SetActive(false);
                _leaderboardObject.SetActive(true);
            }
            else
            {
                _noScoreObject.SetActive(true);
                _leaderboardObject.SetActive(false);
            }
        }

        [UsedImplicitly]
        [UIAction("selectcell")]
        private void OnCellClick(SegmentedControl? _, int index) => ChangeView(index);

        [UsedImplicitly]
        [UIAction("show-modal")]
        private void ShowModal()
        {
            _modal.Show(true);
        }

        internal class EventScoreData : LeaderboardTableView.ScoreData
        {
            public EventScoreData(int score, string playerName, int rank, bool fullCombo, Color? color)
                : base(score, playerName, rank, fullCombo)
            {
                Color = color;
            }

            public Color? Color { get; }
        }
    }
}
