using System;
using System.Collections.Generic;
using System.Linq;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using JetBrains.Annotations;
using Synapse.Managers;
using Synapse.Networking.Models;
using TMPro;
using UnityEngine.UI;
using Zenject;

namespace Synapse.Views;

[ViewDefinition("Synapse.Resources.DivisionSelect.bsml")]
internal class EventDivisionSelectViewController : BSMLAutomaticViewController
{
    [UIComponent("segments")]
    private readonly TextSegmentedControl _textSegments = null!;

    [UIComponent("continue-button")]
    private readonly Button _continueButton = null!;

    [UIComponent("description")]
    private readonly TextMeshProUGUI _descriptionText = null!;

    private ListingManager _listingManager = null!;

    private Action<int>? _callback;

    private List<string> _divisionNames = [];
    private List<Division> _divisions = [];

    private int? _selected;

    public void SetCallback(Action<int> callback)
    {
        _callback = callback;
    }

#if !V1_29_1
    protected override void OnDestroy()
#else
    public override void OnDestroy()
#endif
    {
        base.OnDestroy();
        _listingManager.ListingFound -= OnListingFound;
    }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);

        if (firstActivation)
        {
            DestroyImmediate(_textSegments.gameObject.GetComponent<HorizontalLayoutGroup>());
            VerticalLayoutGroup vertical = _textSegments.gameObject.AddComponent<VerticalLayoutGroup>();
            vertical.childControlHeight = false;
            vertical.childForceExpandHeight = false;
            _textSegments._fontSize = 6;
        }

        // ReSharper disable once InvertIf
        if (addedToHierarchy)
        {
            _textSegments.SetTexts(_divisionNames);
            _continueButton.interactable = false;
            _descriptionText.text = "Select your difficulty";
            for (int index = 0; index < _textSegments._numberOfCells; ++index)
            {
                _textSegments.cells[index].SetSelected(false, SelectableCell.TransitionType.Instant, _textSegments, false);
            }
        }
    }

    [UsedImplicitly]
    [Inject]
    private void Construct(ListingManager listingManager)
    {
        _listingManager = listingManager;
        listingManager.ListingFound += OnListingFound;
    }

    [UsedImplicitly]
    [UIAction("selectcell")]
    private void OnCellClick(SegmentedControl segmentedControl, int index)
    {
        // stupid workaround is stupid
        segmentedControl.cells[index].SetSelected(true, SelectableCell.TransitionType.Instant, segmentedControl, false);
        _selected = index;
        _descriptionText.text = _divisions[index].Description;
        _continueButton.interactable = true;
    }

    [UsedImplicitly]
    [UIAction("continue-click")]
    private void OnContinueClick()
    {
        if (_selected == null)
        {
            return;
        }

        _callback?.Invoke(_selected.Value);
    }

    private void OnListingFound(Listing? listing)
    {
        if (listing == null)
        {
            return;
        }

        _divisions = listing.Divisions;
        _divisionNames = _divisions.Select(n => n.Name).ToList();
        _wasActivatedBefore = false;
    }
}
