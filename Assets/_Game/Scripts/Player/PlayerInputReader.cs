using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    /// <summary>
    /// Collects the hunter's input from every source each frame: the on-screen stick and buttons (they drive a
    /// virtual gamepad), the touch look area, a real gamepad, and keyboard for editor testing
    /// (WASD move, arrow keys look, C crouch).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        InputAction moveAction, lookAction, crouchAction;

        /// <summary>Move direction, length 0..1 (how far the stick is pushed).</summary>
        public Vector2 Move { get; private set; }
        /// <summary>Drag in screen pixels this frame (touch or mouse).</summary>
        public Vector2 LookDrag { get; private set; }
        /// <summary>Stick-style look, -1..1 per axis (gamepad right stick, arrow keys).</summary>
        public Vector2 LookStick { get; private set; }
        public bool CrouchPressed { get; private set; }

        void OnEnable()
        {
            moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            moveAction.AddBinding("<Gamepad>/leftStick");

            lookAction = new InputAction("Look", InputActionType.Value, expectedControlType: "Vector2");
            lookAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            lookAction.AddBinding("<Gamepad>/rightStick");

            crouchAction = new InputAction("Crouch", InputActionType.Button);
            crouchAction.AddBinding("<Keyboard>/c");
            crouchAction.AddBinding("<Gamepad>/buttonEast");

            moveAction.Enable();
            lookAction.Enable();
            crouchAction.Enable();
        }

        void OnDisable()
        {
            moveAction?.Dispose();
            lookAction?.Dispose();
            crouchAction?.Dispose();
            moveAction = lookAction = crouchAction = null;
            Move = LookDrag = LookStick = Vector2.zero;
            CrouchPressed = false;
        }

        void Update()
        {
            Move = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
            LookStick = Vector2.ClampMagnitude(lookAction.ReadValue<Vector2>(), 1f);
            LookDrag = TouchLookArea.ConsumeDelta();
            CrouchPressed = crouchAction.WasPressedThisFrame();
        }
    }
}
