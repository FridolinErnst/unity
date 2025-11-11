using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using Unity.VisualScripting;
using UnityEngine;
using Unity.Netcode;

namespace Kart
{

    public class GameConstants
    {
        public const string k_AxisNameHorizontal = ".Horizontal";
        public const string k_AxisNameVertical = ".Vertical";
        public const string k_ButtonNameBreak = ".Break";
        public const string k_ButtonNameBoost = ".Boost";
        public const string k_MouseAxisNameVertical = ".Mouse Y";
        public const string k_MouseAxisNameHorizontal = ".Mouse X";
    }
    [System.Serializable]
    public struct Inputs
    {
        public Vector2 movement;
        public Vector2 look;
        public float boost_time;
        public float break_time;
    }
    public class PlayerInputHandlerEx4 : NetworkBehaviour
    {
        public float lookSensitivity = 1f;

        public float iriggerAxisThreshold = 0.4f;

        public bool invertYAxis = false;

        public bool invertXAxis = false;

        public Controller controller;
        public List<CarControllerEx4> characters = new List<CarControllerEx4>();

        public bool debug = false;

        public float maxAllowedSpeed = 60f;

        void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private Inputs m_inputs = new Inputs();

        public Inputs Inputs => m_inputs;

        private void Update()
        {
            if (!IsOwner || !IsClient)
            {
                return;
            }
            m_inputs.movement = GetMoveInput();
            m_inputs.look = GetLookInput();
            if (GetBoostInputHeld())
            {
                m_inputs.boost_time += Time.deltaTime;
            }
            else
            {
                m_inputs.boost_time = 0.0f;
            }
            if (GetBreakInputHeld())
            {
                m_inputs.break_time += Time.deltaTime;
            }
            else
            {
                m_inputs.break_time = 0.0f;
            }

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
            return Cursor.lockState == CursorLockMode.Locked/* && !m_GameFlowManager.GameIsEnding*/;
        }

        public Vector2 GetMoveInput()
        {
            if (CanProcessInput())
            {
                Vector2 move = new Vector2(
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
                Vector2 look = new Vector2(
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
            if (CanProcessInput())
            {
                return Input.GetButton(controller.prefix + GameConstants.k_ButtonNameBoost);
            }

            return false;
        }

        public bool GetBreakInputHeld()
        {
            if (CanProcessInput())
            {
                return Input.GetButton(controller.prefix + GameConstants.k_ButtonNameBreak);
            }

            return false;
        }
    }
}