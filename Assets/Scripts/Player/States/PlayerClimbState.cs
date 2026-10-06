using SonTinhThuyTinh.Core;
using UnityEngine;

namespace SonTinhThuyTinh.Player.States
{
    // Climbs up onto the ledge found by LedgeProbe (Space next to a wall): the character steps in to the wall, then hangs on it and pulls itself
    // over the edge, while the position follows the root curves measured from the Climb clip, stretched to the real ledge height. The character
    // controller is switched off for it (the body passes through the edge) and back on at the top.
    public sealed class PlayerClimbState : IState
    {
        static readonly int ClimbHash = Animator.StringToHash("Climb");
        static readonly int LocomotionHash = Animator.StringToHash("Locomotion");

        readonly PlayerController player;
        LedgeProbe.Ledge ledge;
        Vector3 start;
        float elapsed;
        float standUpElapsed;
        bool standingUp;

        public PlayerClimbState(PlayerController player) => this.player = player;

        public void Begin(LedgeProbe.Ledge found) => ledge = found;

        public void Enter()
        {
            ClimbSettings s = player.Climb;
            elapsed = 0f;
            standUpElapsed = 0f;
            standingUp = false;
            start = player.transform.position;

            player.SetBodyEnabled(false);
            player.SetSpeed(0f);
            player.SetAnimSpeed(s.speed);
            player.Animator.CrossFadeInFixedTime(ClimbHash, s.blendIn);
        }

        public void Tick(float deltaTime)
        {
            ClimbSettings s = player.Climb;
            Vector3 into = -ledge.Normal;
            player.FaceTowards(into, 900f * deltaTime);

            if (standingUp)
            {
                standUpElapsed += deltaTime;
                player.transform.position = At(1f);
                if (standUpElapsed >= s.blendOut) player.ChangeState(player.LocomotionState);
                return;
            }

            elapsed += deltaTime;
            float u = Mathf.Clamp01(elapsed * s.speed / s.duration);
            player.transform.position = At(u);

            if (u >= 1f)
            {
                // the clip ends crouched on the ledge: ease back to standing while the position settles onto the ground
                standingUp = true;
                player.SetAnimSpeed(1f);
                player.Animator.CrossFadeInFixedTime(LocomotionHash, s.blendOut);
            }
        }

        // The character's position at normalized clip time u.
        Vector3 At(float u)
        {
            ClimbSettings s = player.Climb;
            float scale = ledge.Height / s.clipHeight;

            // along the wall normal: how far in front of the wall face (negative = already over the edge)
            float inFront = s.wallDistance - s.forward.Evaluate(u) - s.landingExtra * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1f, u));
            Vector3 target = ledge.WallPoint + ledge.Normal * inFront;

            // first the character steps in from wherever it stood
            float step = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(s.approachTime, 0.01f)));
            Vector3 flat = Vector3.Lerp(new Vector3(start.x, 0f, start.z), new Vector3(target.x, 0f, target.z), step);

            float y = start.y + scale * s.rise.Evaluate(u);
            if (standingUp) y = ledge.TopY;
            return new Vector3(flat.x, y, flat.z);
        }

        public void Exit()
        {
            Vector3 p = player.transform.position;
            ClimbSettings s = player.Climb;
            Vector3 onTop = ledge.WallPoint + ledge.Normal * (s.wallDistance - s.forward.Evaluate(1f) - s.landingExtra);
            if (standingUp) player.transform.position = new Vector3(onTop.x, ledge.TopY, onTop.z);   // the exact top (not the eased height) before the controller wakes up
            else player.transform.position = p;
            player.SetBodyEnabled(true);
            player.SetSpeed(0f);
            player.SetAnimSpeed(1f);
            player.ClimbEnded();
            player.InputReader.ClearBuffered();
        }
    }
}
