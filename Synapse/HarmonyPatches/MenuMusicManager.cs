using SiraUtil.Affinity;

namespace Synapse.HarmonyPatches;

internal class MenuMusicManager : IAffinity
{
    private readonly SongPreviewPlayer _songPreviewPlayer;
    private bool _menuMusicDisabled;

    private MenuMusicManager(SongPreviewPlayer songPreviewPlayer)
    {
        _songPreviewPlayer = songPreviewPlayer;
    }

    internal bool MenuMusicDisabled
    {
        ////get => _menuMusicDisabled;
        set
        {
            if (_menuMusicDisabled == value)
            {
                return;
            }

            _menuMusicDisabled = value;
            if (value)
            {
                _songPreviewPlayer.FadeOut(1);
            }
            else
            {
                _songPreviewPlayer.CrossfadeToDefault();
            }
        }
    }

    [AffinityPrefix]
    [AffinityPatch(typeof(SongPreviewPlayer), nameof(SongPreviewPlayer.CrossfadeToDefault))]
    private bool DisableCrossfade()
    {
        return !_menuMusicDisabled;
    }
}
