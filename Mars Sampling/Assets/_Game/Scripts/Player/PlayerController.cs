using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ISTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace MarsSampling
{
    /// <summary>
    /// First-person controller with two input schemes, picked at runtime:
    ///
    /// Touch (phones):
    ///  - A touch starting on the LEFT ~45% of the screen becomes a floating
    ///    joystick (visuals appear where the thumb lands).
    ///  - Any other touch drags to look around.
    ///  - A short tap (quick, barely moved, not on UI) is forwarded to
    ///    PlayerInteractor as an interact tap.
    ///
    /// Desktop (Windows build + editor):
    ///  - WASD / arrows move, mouse looks while the cursor is locked.
    ///  - Left Click or E interacts with whatever the crosshair is on.
    ///  - Tab toggles the tablet, Esc frees the cursor (click the world to re-lock).
    ///  - The cursor is released automatically while any panel/dialogue is open
    ///    (InputLocked) and re-captured when it closes.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Tuning")]
        public float moveSpeed = 5f;
        public float lookSensitivity = 0.16f;   // degrees per pixel dragged (touch)
        public float mouseSensitivity = 0.10f;  // degrees per pixel of mouse delta
        public float gravity = -25f;
        public float joystickRadiusPx = 140f;   // full-deflection thumb travel
        public float tapMaxSeconds = 0.30f;
        public float tapMaxDragPx = 28f;

        [Header("Wired by the scene builder")]
        public Transform cameraPivot;           // camera parent, pitches up/down
        public PlayerInteractor interactor;
        public RectTransform joystickBase;      // HUD visuals for the floating stick
        public RectTransform joystickKnob;

        /// <summary>True while a dialogue / scanner / tablet panel is open.</summary>
        public bool InputLocked { get; set; }

        CharacterController _cc;
        float _pitch;
        float _yVel;
        bool _desktop;      // keyboard/mouse scheme (anything that isn't a phone)
        bool _cursorFreed;  // player pressed Esc; cursor stays free until they click the world

        int _moveTouchId = -1;
        int _lookTouchId = -1;
        Vector2 _moveStart, _moveCurrent, _lookPrev;

        class TapTrack { public int id; public Vector2 start; public float time; public float maxDragSq; }
        readonly List<TapTrack> _taps = new List<TapTrack>();

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _desktop = !Application.isMobilePlatform;
            SetJoystickVisible(false);
        }

        void Update()
        {
            Vector2 moveInput = Vector2.zero;
            Vector2 lookDelta = Vector2.zero;

            ReadTouches(ref moveInput, ref lookDelta);
            if (_desktop)
            {
                ReadDesktopInput(ref moveInput, ref lookDelta);
                UpdateCursorLock();
            }

            if (InputLocked)
            {
                moveInput = Vector2.zero;
                lookDelta = Vector2.zero;
                SetJoystickVisible(false);
            }

            // Look: yaw the body, pitch the camera pivot.
            transform.Rotate(0f, lookDelta.x * lookSensitivity, 0f);
            _pitch = Mathf.Clamp(_pitch - lookDelta.y * lookSensitivity, -80f, 80f);
            if (cameraPivot != null)
                cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);

            // Move: joystick/WASD relative to facing, plus simple gravity.
            Vector3 planar = (transform.right * moveInput.x + transform.forward * moveInput.y) * moveSpeed;
            if (_cc.isGrounded) _yVel = -2f;
            else _yVel += gravity * Time.deltaTime;
            _cc.Move((planar + Vector3.up * _yVel) * Time.deltaTime);
        }

        // ------------------------------------------------------------------ touch

        void ReadTouches(ref Vector2 moveInput, ref Vector2 lookDelta)
        {
            var ts = Touchscreen.current;
            if (ts == null) return;

            foreach (var touch in ts.touches)
            {
                var phase = touch.phase.ReadValue();
                if (phase == ISTouchPhase.None) continue;

                int id = touch.touchId.ReadValue();
                Vector2 pos = touch.position.ReadValue();

                if (phase == ISTouchPhase.Began)
                {
                    if (UiUtil.IsPointerOverUi(pos)) continue; // UI owns this touch entirely

                    // Track the touch as a potential interact tap.
                    _taps.Add(new TapTrack { id = id, start = pos, time = Time.unscaledTime });

                    if (InputLocked) continue;

                    if (pos.x < Screen.width * 0.45f && _moveTouchId == -1)
                    {
                        _moveTouchId = id;
                        _moveStart = _moveCurrent = pos;
                        SetJoystickVisible(true);
                    }
                    else if (_lookTouchId == -1)
                    {
                        _lookTouchId = id;
                        _lookPrev = pos;
                    }
                }

                // Update tap-candidate drag distance.
                for (int i = 0; i < _taps.Count; i++)
                {
                    if (_taps[i].id != id) continue;
                    float dragSq = (pos - _taps[i].start).sqrMagnitude;
                    if (dragSq > _taps[i].maxDragSq) _taps[i].maxDragSq = dragSq;
                }

                bool released = phase == ISTouchPhase.Ended || phase == ISTouchPhase.Canceled;

                if (id == _moveTouchId)
                {
                    _moveCurrent = pos;
                    if (released) { _moveTouchId = -1; SetJoystickVisible(false); }
                }
                else if (id == _lookTouchId)
                {
                    lookDelta += pos - _lookPrev;
                    _lookPrev = pos;
                    if (released) _lookTouchId = -1;
                }

                if (released) ResolveTap(id, pos, phase == ISTouchPhase.Ended);
            }

            if (_moveTouchId != -1)
            {
                Vector2 raw = (_moveCurrent - _moveStart) / joystickRadiusPx;
                moveInput += Vector2.ClampMagnitude(raw, 1f);
                UpdateJoystickVisual(moveInput);
            }
        }

        void ResolveTap(int id, Vector2 pos, bool cleanEnd)
        {
            for (int i = _taps.Count - 1; i >= 0; i--)
            {
                if (_taps[i].id != id) continue;
                var t = _taps[i];
                _taps.RemoveAt(i);
                if (!cleanEnd) return;
                bool quick = Time.unscaledTime - t.time <= tapMaxSeconds;
                bool still = t.maxDragSq <= tapMaxDragPx * tapMaxDragPx;
                if (quick && still && interactor != null)
                    interactor.TapAt(pos);
                return;
            }
        }

        // ---------------------------------------------------------------- desktop

        void ReadDesktopInput(ref Vector2 moveInput, ref Vector2 lookDelta)
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            Vector2 centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            bool cursorLocked = Cursor.lockState == CursorLockMode.Locked;

            if (kb != null)
            {
                Vector2 wasd = Vector2.zero;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) wasd.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) wasd.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) wasd.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) wasd.x -= 1f;
                moveInput += Vector2.ClampMagnitude(wasd, 1f);

                // E = interact with whatever the crosshair is on.
                if (kb.eKey.wasPressedThisFrame && interactor != null && !InputLocked)
                    interactor.TapAt(centre);

                // Tab = tablet. Esc = close the tablet if open, otherwise free the cursor.
                var mission = MissionManager.Instance;
                if (kb.tabKey.wasPressedThisFrame && mission != null)
                    mission.tablet.Toggle();
                if (kb.escapeKey.wasPressedThisFrame)
                {
                    if (mission != null && mission.tablet.IsOpen) mission.tablet.Close();
                    else _cursorFreed = true;
                }
            }

            if (mouse != null)
            {
                // Mouse look whenever the cursor is captured. Holding Right Mouse also
                // works with a free cursor (handy in the editor).
                if (cursorLocked || mouse.rightButton.isPressed)
                    lookDelta += mouse.delta.ReadValue() * (mouseSensitivity / lookSensitivity);

                if (mouse.leftButton.wasPressedThisFrame && !InputLocked)
                {
                    if (cursorLocked)
                    {
                        if (interactor != null) interactor.TapAt(centre);
                    }
                    else if (!UiUtil.IsPointerOverUi(mouse.position.ReadValue()))
                    {
                        _cursorFreed = false; // clicking the world re-captures the cursor
                    }
                }
            }
        }

        /// <summary>Capture the cursor for mouse-look unless a panel is open or Esc freed it.</summary>
        void UpdateCursorLock()
        {
            bool wantLock = !InputLocked && !_cursorFreed;
            var mode = wantLock ? CursorLockMode.Locked : CursorLockMode.None;
            if (Cursor.lockState != mode)
            {
                Cursor.lockState = mode;
                Cursor.visible = !wantLock;
            }
        }

        // ------------------------------------------------------------------ HUD

        void SetJoystickVisible(bool on)
        {
            if (joystickBase != null && joystickBase.gameObject.activeSelf != on)
                joystickBase.gameObject.SetActive(on);
        }

        void UpdateJoystickVisual(Vector2 input)
        {
            if (joystickBase == null || joystickKnob == null) return;
            // Screen-space overlay canvas: RectTransform.position is in pixels.
            joystickBase.position = _moveStart;
            joystickKnob.position = _moveStart + input * (joystickRadiusPx * 0.6f);
        }
    }
}
