using UnityEngine;

namespace Game.Animals
{
    /// <summary>
    /// Sits next to an animal's Animator and swallows the clips' root motion. On a Generic rig with Apply Root Motion
    /// off, Unity plays the root bone's travel inside the pose, so a walking body creeps ahead of the animal and snaps
    /// back every loop. With root motion on and this handler present, the travel is extracted and dropped here; the
    /// navigation agent alone moves the animal.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class RootMotionSink : MonoBehaviour
    {
        void Awake() => GetComponent<Animator>().applyRootMotion = true;

        void OnAnimatorMove() { }   // having this callback means Unity hands root motion to us instead of moving the transform
    }
}
