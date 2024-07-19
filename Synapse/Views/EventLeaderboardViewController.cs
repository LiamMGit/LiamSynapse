using System.Collections.Generic;
using System.Linq;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using HarmonyLib;
using HMUI;
using IPA.Utilities.Async;
using JetBrains.Annotations;
using Synapse.Managers;
using Synapse.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using Random = UnityEngine.Random;

namespace Synapse.Views;

[ViewDefinition("Synapse.Resources.Leaderboard.bsml")]
internal class EventLeaderboardViewController : BSMLAutomaticViewController
{
    private static readonly string[] _randomMotivationals =
    [
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
    ];

    [UIComponent("header")]
    private readonly ImageView _header = null!;

    [UIObject("leaderboard")]
    private readonly GameObject _leaderboardObject = null!;

    private readonly Dictionary<int, LeaderboardScores> _leaderboardScores = new();

    [UIComponent("modal")]
    private readonly ModalView _modal = null!;

    [UIComponent("motivation")]
    private readonly TextMeshProUGUI _motivational = null!;

    [UIObject("noscore")]
    private readonly GameObject _noScoreObject = null!;

    [UIComponent("vertical")]
    private readonly VerticalLayoutGroup _root = null!;

    [UIComponent("segments")]
    private readonly TextSegmentedControl _textSegments = null!;

    [UIComponent("titlelayout")]
    private readonly LayoutElement _titleLayout = null!;

    [UIComponent("titlemap")]
    private readonly TextMeshProUGUI _titleMapText = null!;

    private bool _altCover;
    private Config _config = null!;

    private bool _dirtyTextSegments;
    private int _index;

    private LeaderboardTableView _leaderboardTable = null!;
    private LoadingControl _loadingControl = null!;
    private MapDownloadingManager _mapDownloadingManager = null!;

    private int _maxIndex;

    private NetworkManager _networkManager = null!;
    private string[] _textSegmentTexts = [];

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

            DestroyImmediate(_textSegments.gameObject.GetComponent<HorizontalLayoutGroup>());
            VerticalLayoutGroup vertical = _textSegments.gameObject.AddComponent<VerticalLayoutGroup>();
            vertical.childControlHeight = false;
            vertical.childForceExpandHeight = false;
            _textSegments._fontSize = 4;

            _titleLayout.flexibleHeight = 0;

            // for some reason rebuilding it twice fixes it???
            RectTransform rootRect = (RectTransform)_root.transform;
            LayoutRebuilder.MarkLayoutForRebuild(rootRect);
        }

        if (addedToHierarchy)
        {
            _leaderboardScores.Clear();
            ChangeView(_maxIndex);
            _mapDownloadingManager.MapDownloaded += OnMapDownloaded;
        }

        _motivational.text = _randomMotivationals[Random.Range(0, _randomMotivationals.Length - 1)];
    }

    protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
    {
        base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);

        // ReSharper disable once InvertIf
        if (removedFromHierarchy)
        {
            _mapDownloadingManager.MapDownloaded -= OnMapDownloaded;
        }
    }

    private void AssignScores(LeaderboardScores scores)
    {
        bool useAlt = _altCover &&
                      _networkManager.Status.Stage is PlayStatus playStatus &&
                      scores.Index >= playStatus.Index &&
                      playStatus.PlayerScore == null;
        _titleMapText.text = useAlt ? "???" : scores.Title;
        List<LeaderboardCell> cells = ShowEliminated ? scores.ElimScores : scores.Scores;
        if (cells.Count > 0)
        {
            List<LeaderboardTableView.ScoreData> data = cells
                .Select(
                    n =>
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
                            n.Percentage,
                            color);
                    })
                .ToList();
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

    [UsedImplicitly]
    [Inject]
    private void Construct(
        NetworkManager networkManager,
        MapDownloadingManager mapDownloadingManager,
        Config config)
    {
        _networkManager = networkManager;
        networkManager.LeaderboardReceived += OnLeaderboardReceived;
        networkManager.MapUpdated += OnMapUpdated;
        _mapDownloadingManager = mapDownloadingManager;
        _config = config;
    }

    [UsedImplicitly]
    [UIAction("selectcell")]
    private void OnCellClick(SegmentedControl? _, int index)
    {
        ChangeView(index);
    }

    private void OnLeaderboardReceived(LeaderboardScores leaderboardScores)
    {
        _leaderboardScores[leaderboardScores.Index] = leaderboardScores;
        UnityMainThreadTaskScheduler.Factory.StartNew(
            () =>
            {
                if (leaderboardScores.Index != _index)
                {
                    return;
                }

                _loadingControl.Hide();
                AssignScores(leaderboardScores);
            });
    }

    private void OnMapDownloaded(DownloadedMap map)
    {
        _altCover = !string.IsNullOrWhiteSpace(map.Map.AltCoverUrl);
    }

    private void OnMapUpdated(int index, Map? map)
    {
        if (map == null)
        {
            return;
        }

        _altCover = true;
        _maxIndex = index;
        _textSegmentTexts = Enumerable.Range(1, index + 1).Select(n => n.ToString()).ToArray();
        _dirtyTextSegments = true;
    }

    private void Refresh()
    {
        _loadingControl.ShowLoading();
        _leaderboardTable.SetScores(null, -1);
        _ = _networkManager.Send(ServerOpcode.LeaderboardRequest, _index);
    }

    [UsedImplicitly]
    [UIAction("show-modal")]
    private void ShowModal()
    {
        _modal.Show(true);
    }

    private void Update()
    {
        if (!_dirtyTextSegments)
        {
            return;
        }

        _dirtyTextSegments = false;

        // Have to be destroyed with DestroyImmediate otherwise the leaderboard sinks
        List<SegmentedControlCell> cells = _textSegments._cells;
        List<GameObject> separators = _textSegments._separators;

        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        cells.Where(n => n != null && n.gameObject != null).Do(n => DestroyImmediate(n.gameObject));
        cells.Clear();
        separators.ForEach(DestroyImmediate);
        separators.Clear();

        if (_index >= _textSegmentTexts.Length)
        {
            _index = 0;
        }

        _textSegments.SetTexts(_textSegmentTexts);
        _textSegments.SelectCellWithNumber(_index);
    }

    internal class EventScoreData(int score, string playerName, int rank, float percentage, Color? color)
        : LeaderboardTableView.ScoreData(score, playerName, rank, false)
    {
        public Color? Color { get; } = color;

        public float Percentage { get; } = percentage;
    }
}
