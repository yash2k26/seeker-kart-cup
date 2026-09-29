using UnityEngine;

namespace KartCup
{
    /// One racer on the podium. Second and third hold their trophies up beside their heads and sway; the winner jumps
    /// on the spot with both arms up and the gold trophy held high over their head.
    public sealed class PodiumPose : MonoBehaviour
    {
        PodiumShow.Rig rig;
        Transform armLeft, armRight;
        Quaternion holdLeft = Quaternion.identity, holdRight = Quaternion.identity;   // the arms in the hold pose
        bool winner;
        Vector3 home;
        float phase;

        bool raiseRight, raiseLeft;
        float pump;                                   // the winner's arms: 0 out beside the chin, 1 up in the V

        public void Init(int place, PodiumShow.Rig r)
        {
            rig = r; winner = place == 0;
            raiseRight = place != 2;                  // second holds it on its right, third on its left, the winner both
            raiseLeft = place != 1;
            home = transform.position;
            phase = place * 1.7f;
            foreach (var x in GetComponentsInChildren<Transform>(true))
            {
                if (x.name == "arm-left") { armLeft = x; holdLeft = x.localRotation; }
                else if (x.name == "arm-right") { armRight = x; holdRight = x.localRotation; }
            }
        }

        void LateUpdate() => Apply(Time.unscaledTime);

        /// This racer's pose at time `t`, from the hold pose Init saw (set outright, so it never builds up).
        public void Apply(float time)
        {
            float t = time + phase;
            if (!winner)
            {
                // A proud little sway and the trophy arm up, never a jump: the jumping is the winner's.
                transform.position = home;
                transform.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * 1.6f) * 6f, 0f) * BaseYaw();
                RaiseArms();
                return;
            }
            // Hop after hop, landing with a squash, fists pumping with every hop: out on the landing (clear of the big
            // head, so they read), up in a V at the top.
            const float period = 0.52f;
            float u = (t % period) / period;
            float hop = Mathf.Sin(u * Mathf.PI);
            pump = hop;
            BaseYaw();
            transform.position = home + Vector3.up * hop * 0.55f;
            float squash = u < 0.12f || u > 0.88f ? 0.9f : 1f;
            transform.localScale = new Vector3(1f / Mathf.Sqrt(squash), squash, 1f / Mathf.Sqrt(squash)) * baseScale;
            transform.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * 2.2f) * 10f, 0f) * BaseYaw();
            RaiseArms();
        }

        void RaiseArms()
        {
            if (armLeft != null) armLeft.localRotation = raiseLeft ? holdLeft * (winner ? Quaternion.Slerp(rig.outLeft, rig.raiseLeft, pump) : rig.outLeft) : holdLeft;
            if (armRight != null) armRight.localRotation = raiseRight ? holdRight * (winner ? Quaternion.Slerp(rig.outRight, rig.raiseRight, pump) : rig.outRight) : holdRight;
        }

        Quaternion baseYaw;
        float baseScale = 1f;
        bool captured;
        Quaternion BaseYaw()
        {
            if (!captured) { baseYaw = transform.localRotation; baseScale = transform.localScale.x; captured = true; }
            return baseYaw;
        }

        void Awake() { BaseYaw(); }
    }
}
