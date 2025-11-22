using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Kart
{
    public class PlayerInputHandlerEx5 : NetworkBehaviour
    {
        public float lookSensitivity = 1f;

        public float iriggerAxisThreshold = 0.4f;

        public bool invertYAxis;

        public bool invertXAxis;

        public Controller controller;
        public List<CarControllerEx4> characters = new();

        public bool debug;

        public float maxAllowedSpeed = 60f;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private Inputs m_inputs;

        public Inputs Inputs => m_inputs;

        private void Update()
        {
            if (!IsOwner) return;
            m_inputs.movement = GetMoveInput();
            m_inputs.look = GetLookInput();
            if (GetBoostInputHeld())
                m_inputs.boost_time += Time.deltaTime;
            else
                m_inputs.boost_time = 0.0f;
            if (GetBreakInputHeld())
                m_inputs.break_time += Time.deltaTime;
            else
                m_inputs.break_time = 0.0f;

            if (debug)
            {
                if (m_inputs.movement != Vector2.zero) Debug.Log("GetMoveInput: " + m_inputs.movement);
                if (m_inputs.look != Vector2.zero) Debug.Log("GetLookInput: " + m_inputs.look);
                if (m_inputs.boost_time != 0) Debug.Log("GetBoostInputHeld: " + m_inputs.boost_time);
                if (m_inputs.break_time != 0) Debug.Log("GetBreakInputHeld: " + m_inputs.break_time);
            }
            //update character controllers
            //foreach (CarControllerEx4 character in characters)
            //{
            //   character.ApplyInputs(m_inputs, Time.deltaTime);
            //}
        }


        public bool CanProcessInput()
        {
            return Cursor.lockState == CursorLockMode.Locked /* && !m_GameFlowManager.GameIsEnding*/;
        }

        public Vector2 GetMoveInput()
        {
            if (CanProcessInput())
            {
                var move = new Vector2(
                    Input.GetAxisRaw(controller.prefix + GameConstants.k_AxisNameHorizontal),
                    Input.GetAxisRaw(controller.prefix + GameConstants.k_AxisNameVertical)
                );

                // constrain move input to a maximum magnitude of 1, otherwise diagonal movement might exceed the max move speed defined
                // not needed for racing games
                //move = Vector2.ClampMagnitude(move, 1);

                return move;
            }

            return Vector2.zero;
        }

        public Vector2 GetLookInput()
        {
            if (CanProcessInput())
            {
                var look = new Vector2(
                    Input.GetAxisRaw(controller.prefix + GameConstants.k_MouseAxisNameHorizontal),
                    Input.GetAxisRaw(controller.prefix + GameConstants.k_MouseAxisNameVertical)
                );

                // handle inverting vertical input
                if (invertXAxis)
                    look *= new Vector2(-1f, 1);
                if (invertYAxis)
                    look *= new Vector2(1, -1f);

                // apply sensitivity multiplier
                look *= lookSensitivity;

                return look;
            }

            return Vector2.zero;
        }

        public bool GetBoostInputHeld()
        {
            if (CanProcessInput()) return Input.GetButton(controller.prefix + GameConstants.k_ButtonNameBoost);

            return false;
        }

        public bool GetBreakInputHeld()
        {
            if (CanProcessInput()) return Input.GetButton(controller.prefix + GameConstants.k_ButtonNameBreak);

            return false;
        }
    }
}