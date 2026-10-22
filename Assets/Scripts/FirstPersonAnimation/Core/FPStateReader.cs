using UnityEngine;
using EasyPeasyFirstPersonController;
namespace FPAnimation
{
    public enum FPState { Grounded, Jumping, Sliding, Crouching, Fall, LedgeGrab, Swimming }

    /// <summary>Snapshot of everything the layers need this frame.</summary>
    public struct FPLoco
    {
        public FPState state;
        public float stateTime, speed, vy, landSpeed, slideT;
        public Vector3 localVel;       // relative to player yaw: x strafe, z forward
        public bool airborne, justLanded, sprinting;
        public Vector2 move, look;
    }

    /// <summary>Polls the controller. Never edits it.</summary>
    public class FPStateReader
    {
        public FPLoco V;
        PlayerBaseState last;
        float airTimer, minVy;
        bool wasAir;

        static FPState Classify(PlayerBaseState s)
        {
            if (s is PlayerJumpingState) return FPState.Jumping;
            if (s is PlayerFallState) return FPState.Fall;
            if (s is PlayerSlidingState) return FPState.Sliding;
            if (s is PlayerCrouchingState) return FPState.Crouching;
            if (s is PlayerLedgeGrabState) return FPState.LedgeGrab;
            if (s is PlayerSwimmingState) return FPState.Swimming;
            return FPState.Grounded;
        }

        public void Update(FirstPersonController c, float dt, FPAnimSettings s)
        {
            var cs = c.CurrentState;
            if (cs != last) { last = cs; V.state = Classify(cs); V.stateTime = 0f; }
            else V.stateTime += dt;

            Vector3 vel = c.characterController.velocity;
            V.localVel = c.transform.InverseTransformDirection(vel);
            V.vy = vel.y;
            V.speed = new Vector2(vel.x, vel.z).magnitude;
            var st = V.state;

            bool special = st == FPState.Swimming || st == FPState.LedgeGrab;
            if (c.isGrounded || special) airTimer = 0f; else airTimer += dt;
            V.airborne = !special && (st == FPState.Jumping || st == FPState.Fall || airTimer > s.groundGrace);

            V.justLanded = false;
            if (V.airborne) { wasAir = true; minVy = Mathf.Min(minVy, vel.y); }
            else
            {
                if (wasAir && !special) { V.justLanded = true; V.landSpeed = -minVy; }
                wasAir = false; minVy = 0f;
            }

            var inp = c.input;
            V.move = inp != null ? inp.moveInput : Vector2.zero;
            V.look = inp != null ? inp.lookInput : Vector2.zero;
            V.sprinting = st == FPState.Grounded && inp != null && inp.sprint && V.speed > c.walkSpeed * 1.15f;
            V.slideT = st == FPState.Sliding ? Mathf.Clamp01(V.stateTime / Mathf.Max(0.01f, c.slideDuration)) : 0f;

            // Seated: the controller is parked, so report a calm, grounded, motionless player. Without this a parked
            // controller reads as "falling" and the guitar / feeding / arm layers would treat the player as disturbed.
            if (FPEvents.Sitting)
            {
                V.state = FPState.Grounded; V.stateTime = 0f;
                V.speed = 0f; V.vy = 0f; V.localVel = Vector3.zero; V.landSpeed = 0f; V.slideT = 0f;
                V.airborne = false; V.justLanded = false; V.sprinting = false;
                V.move = Vector2.zero;
                airTimer = 0f; wasAir = false; minVy = 0f;
            }
        }
    }
}
