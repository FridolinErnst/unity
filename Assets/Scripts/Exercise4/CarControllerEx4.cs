using UnityEditor;
using UnityEngine;

namespace Kart
{
    public class CarControllerEx4 : MonoBehaviour
    {
        public CharacterController character;
        public float rotationSpeed = 90.0f;
        public AnimationCurve accelerationCurve;
        public float maxSpeed = 45f;
        public float minRotSpeed = 5f;
        public float m_speed;
        public float m_turning;
        public float drag = 1.5f;
        public float breakStrenght = 25f;
        private float deltaTime;

        public CarControllerEx4()
        {
            Keyframe keyframe;
            accelerationCurve = new AnimationCurve();

            keyframe = new Keyframe(-1f, 0f);
            accelerationCurve.AddKey(keyframe);

            keyframe = new Keyframe(-0.5f, 1f);
            accelerationCurve.AddKey(keyframe);

            keyframe = new Keyframe(0f, 1.2f); // 0 speed
            accelerationCurve.AddKey(keyframe);

            keyframe = new Keyframe(0.5f, 1f);
            keyframe.outTangent = float.NegativeInfinity;
            accelerationCurve.AddKey(keyframe);

            AnimationUtility.SetKeyRightTangentMode(accelerationCurve, 0, AnimationUtility.TangentMode.Constant);
            AnimationUtility.SetKeyRightTangentMode(accelerationCurve, 1, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyLeftTangentMode(accelerationCurve, 2, AnimationUtility.TangentMode.ClampedAuto);
        }

        public void ApplyInputs(Inputs inputs, float deltaTime)
        {
            this.deltaTime = deltaTime;
            if (inputs.break_time == 0) Accelerate(inputs.movement.y);
            else Break(inputs.break_time);
            Drag(inputs.movement.y);
            Turn(inputs.movement.x);

            m_speed = Mathf.Sign(m_speed) * Mathf.Min(maxSpeed, Mathf.Abs(m_speed));
            character.Move(Quaternion.LookRotation(transform.forward, transform.up) *
                           new Vector3(0, 0, m_speed * deltaTime));
            transform.Rotate(transform.up, m_turning * deltaTime);
        }

        private void Accelerate(float forward)
        {
            var acceleration = accelerationCurve.Evaluate(m_speed / maxSpeed);
            if (Mathf.Sign(m_speed) != Mathf.Sign(forward)) acceleration *= 0.1f;
            m_speed += forward * acceleration;
        }

        private void Break(float breakDuration)
        {
            if (breakDuration != 0f) m_speed -= Mathf.Sign(m_speed) * (breakStrenght + breakDuration) * deltaTime;
        }

        private void Drag(float forward)
        {
            var acceleration = accelerationCurve.Evaluate(m_speed / maxSpeed);
            if (forward == 0 || Mathf.Sign(m_speed) != Mathf.Sign(forward)) m_speed *= 1f - drag * deltaTime;
            if (Mathf.Abs(m_speed) < 0.005f) m_speed = 0f;
        }

        private void Turn(float angle)
        {
            angle *= rotationSpeed;
            angle *= Mathf.Sign(m_speed);
            if (Mathf.Abs(m_speed / minRotSpeed) < 0.5f)
                angle = 0f;
            else if (Mathf.Abs(m_speed / minRotSpeed) < 1.0f) angle *= Mathf.Abs(m_speed / minRotSpeed);
            m_turning = angle;
        }
    }
}