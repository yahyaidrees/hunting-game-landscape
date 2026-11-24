using UnityEngine;

namespace Game.Core.Audio
{
    /// <summary>A region's background sound bed (birds, insects, wind), looping for as long as the scene runs.</summary>
    public sealed class AmbienceLoop : MonoBehaviour
    {
        [SerializeField] SoundCue cue;
        AudioSource playing;

        void Start() => playing = AudioService.PlayAttached(cue, transform);

        void OnDisable()
        {
            AudioService.Stop(playing);
            playing = null;
        }
    }
}
