using UnityEngine;

[DisallowMultipleComponent]
internal sealed class WorldPlayerRootMotion : MonoBehaviour
{
    Animator animator;

    internal WorldPlayerAnimation Owner { private get; set; }

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    void OnAnimatorMove()
    {
        // This callback takes over automatic root motion on the Animator's own GameObject.
        if (Owner != null)
            Owner.CaptureAttackDisplacement(animator.deltaPosition);
        else if (animator.applyRootMotion)
            animator.ApplyBuiltinRootMotion();
    }
}
