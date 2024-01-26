using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using IPA.Utilities.Async;
using SiraUtil.Logging;
using SRT.Managers;
using SRT.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace SRT.Views
{
    [ViewDefinition("SRT.Resources.Leaderboard.bsml")]
    public class EventLeaderboardViewController : BSMLAutomaticViewController
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

        private SiraLog _log = null!;
        private NetworkManager _networkManager = null!;

        private LeaderboardTableView _leaderboardTable = null!;
        private LoadingControl _loadingControl = null!;

        private bool _dirtyTextSegments;
        private string[] _textSegmentTexts;

        private int _index;

        [Inject]
        private void Construct(SiraLog log, NetworkManager networkManager)
        {
            _log = log;
            _networkManager = networkManager;
            networkManager.LeaderboardReceived += OnLeaderboardReceived;
            networkManager.MapUpdated += OnMapUpdated;
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
                Refresh();
            }

            _motivational.text = _randomMotivationals[Random.Range(0, _randomMotivationals.Length - 1)];
        }

        // TODO: cache
        private void Refresh()
        {
            _loadingControl.ShowLoading();
            _leaderboardTable.SetScores(null, -1);
            _ = _networkManager.SendInt(_index, ServerOpcode.LeaderboardRequest);
        }

        private void OnMapUpdated(int index, Map map)
        {
            _textSegmentTexts = Enumerable.Range(1, index + 1).Select(n => n.ToString()).ToArray();
            _dirtyTextSegments = true;
        }

        private void Update()
        {
            if (_dirtyTextSegments)
            {
                _dirtyTextSegments = false;
                _textSegments.SetTexts(_textSegmentTexts);
            }
        }

        private void OnLeaderboardReceived(LeaderboardScores leaderboardScores)
        {
            UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                if (leaderboardScores.Scores.Count > 0)
                {
                    _loadingControl.Hide();
                    List<LeaderboardTableView.ScoreData> data = leaderboardScores.Scores.Select(n =>
                        new LeaderboardTableView.ScoreData(n.Score, n.PlayerName, n.Rank + 1, n.FullCombo)).ToList();
                    _leaderboardTable.SetScores(data, leaderboardScores.PlayerScoreIndex);
                    _noScoreObject.SetActive(false);
                    _leaderboardObject.SetActive(true);
                }
                else
                {
                    _noScoreObject.SetActive(true);
                    _leaderboardObject.SetActive(false);
                }
            });
        }

        [UIAction("selectcell")]
        private void OnCellClick(SegmentedControl? _, int index)
        {
            _index = index;
            Refresh();
        }
    }
}
