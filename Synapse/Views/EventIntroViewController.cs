using System;
using System.Collections;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using JetBrains.Annotations;
using Synapse.Managers;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Synapse.Views;

[ViewDefinition("Synapse.Resources.Intro.bsml")]
internal class EventIntroViewController : BSMLAutomaticViewController
{
    private static readonly int _cancel = Animator.StringToHash("cancel");
    private static readonly int _intro = Animator.StringToHash("intro");

    [UIComponent("circle")]
    private readonly ImageView _circle = null!;

    [UIObject("skip")]
    private readonly GameObject _skipButton = null!;

    private Config _config = null!;

    private Coroutine? _coroutine;

    private bool _doMute;
    private HeldButton _heldButton = null!;
    private MenuPrefabManager _menuPrefabManager = null!;

    internal event Action? Finished;

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);

        if (firstActivation)
        {
            DestroyImmediate(_skipButton.GetComponent<NoTransitionsButton>());
            _heldButton = _skipButton.AddComponent<HeldButton>();
            _heldButton.Init(
                _skipButton.GetComponent<ButtonStaticAnimations>(),
                _skipButton.GetComponent<SignalOnUIButtonClick>());
            _circle.fillMethod = Image.FillMethod.Radial360;
            _circle.type = Image.Type.Filled;
            _circle.fillOrigin = 2;
        }

        // ReSharper disable once InvertIf
        if (addedToHierarchy)
        {
            Animator? animator = _menuPrefabManager.Animator;
            if (animator == null)
            {
                Finished?.Invoke();
            }
            else
            {
                _coroutine = StartCoroutine(PlayAndWaitForEnd(animator));
            }
        }
    }

    protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
    {
        base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);

        // ReSharper disable once InvertIf
        if (removedFromHierarchy)
        {
            if (_coroutine != null)
            {
                StopCoroutine(_coroutine);
            }

            Animator? animator = _menuPrefabManager.Animator;
            if (animator != null)
            {
                animator.SetTrigger(_cancel);
            }
        }
    }

    [UsedImplicitly]
    [Inject]
    private void Construct(Config config, MenuPrefabManager menuPrefabManager)
    {
        _config = config;
        _menuPrefabManager = menuPrefabManager;
    }

    private void Finish()
    {
        _config.MuteMusic = _doMute;
        _config.LastSeenIntro = _menuPrefabManager.LastHash;
        Finished?.Invoke();
    }

    private IEnumerator PlayAndWaitForEnd(Animator animator)
    {
        animator.SetTrigger(_intro);
        _doMute = _config.MuteMusic;
        _config.MuteMusic = false;

        yield return new WaitWhile(() => animator.GetCurrentAnimatorStateInfo(0).normalizedTime <= 1.0f);

        Finish();
    }

    private void Update()
    {
        float dur = _heldButton.HeldDuration;
        _circle.fillAmount = dur;

        if (dur >= 1)
        {
            Finish();
        }
    }

    // some jank i have to do because for some cursed reason,
    // the input system instantly releases any component that implements IPointerClickHandler
    private class HeldButton : Selectable
    {
        private ButtonStaticAnimations? _buttonStaticAnimations;
        private SignalOnUIButtonClick? _signalOnUIButtonClick;

        internal float HeldDuration { get; private set; }

        internal void Init(ButtonStaticAnimations? buttonStaticAnimations, SignalOnUIButtonClick signalOnUIButtonClick)
        {
            _buttonStaticAnimations = buttonStaticAnimations;
            _signalOnUIButtonClick = signalOnUIButtonClick;
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            NoTransitionsButton.SelectionState selectionState = NoTransitionsButton.SelectionState.Normal;
            switch (state)
            {
                case SelectionState.Normal:
                    selectionState = NoTransitionsButton.SelectionState.Normal;
                    break;
                case SelectionState.Highlighted:
                    selectionState = NoTransitionsButton.SelectionState.Highlighted;
                    break;
                case SelectionState.Pressed:
                    selectionState = NoTransitionsButton.SelectionState.Pressed;
                    _signalOnUIButtonClick?._buttonClickedSignal.Raise();
                    break;
                case SelectionState.Disabled:
                    selectionState = NoTransitionsButton.SelectionState.Disabled;
                    break;
            }

            // illegal?
            _buttonStaticAnimations?.HandleButtonSelectionStateDidChange(selectionState);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            HeldDuration = 0;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            HeldDuration = 0;
        }

        private void Update()
        {
            HeldDuration = currentSelectionState == SelectionState.Pressed
                ? Mathf.Min(HeldDuration + Time.unscaledDeltaTime, 1)
                : Mathf.Lerp(HeldDuration, 0, Time.unscaledDeltaTime * 4f);
        }
    }
}
