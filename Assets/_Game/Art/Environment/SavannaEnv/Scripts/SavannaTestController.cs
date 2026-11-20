using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;

namespace SavannaEnv
{
    // Test-only first-person controller for checking the environment on a phone (not gameplay code):
    // drag anywhere outside the joystick zone to look, touch the lower-left of the screen for a
    // floating joystick to walk, and an FPS readout sits at the top. In the editor: hold the left
    // mouse button and drag to look (or drag in the joystick zone to walk), WASD / arrows to walk,
    // Shift to run. The UI is built at runtime (one canvas, three graphics, text updated twice a second).
    // On a phone it picks the quality level the real game would (same rule as QualityManager); tap the FPS text
    // to cycle through the levels. Draw calls and triangles show when the build exposes those counters.
    // The round button at the lower right toggles the sniper zoom (right mouse button in the editor).
    [RequireComponent(typeof(CharacterController))]
    public class SavannaTestController : MonoBehaviour
    {
        public Camera cam;
        public float walkSpeed = 6f;
        [Tooltip("Degrees turned when dragging across the full screen height.")]
        public float lookSpeed = 110f;
        public float maxPitch = 80f;
        [Tooltip("Keeps the player on the ground mesh (radius 300 m).")]
        public float maxRadius = 285f;
        public int targetFrameRate = 60;
        [Tooltip("Vertical field of view through the scope (the game's 3x scope is about 20 degrees).")]
        public float scopeFov = 20f;

        CharacterController cc;
        float yaw, pitch;
        const int MouseId = -100;
        int moveId = int.MinValue, lookId = int.MinValue;
        Vector2 moveOrigin, moveVector, lastLookPos;
        RectTransform stickBase, stickKnob;
        Image baseImage, knobImage;
        Vector2 stickHome;
        float stickRadius;
        Image scopeImage;
        bool scoped;
        float normalFov;
        Text fpsText;
        float fpsTimer, worstFrame;
        int fpsFrames;
        ProfilerRecorder batches, triangles;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            if (!cam) cam = GetComponentInChildren<Camera>();
            Application.targetFrameRate = targetFrameRate;
            if (!Application.isEditor)
            {
                int mem = SystemInfo.systemMemorySize;
                QualitySettings.SetQualityLevel(mem <= 2048 ? 0 : mem <= 4096 ? 1 : 2);
            }
            batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            Input.simulateMouseWithTouches = false;
            Input.multiTouchEnabled = true;
            yaw = transform.eulerAngles.y;
            pitch = Mathf.DeltaAngle(0f, cam.transform.localEulerAngles.x);
            normalFov = cam.fieldOfView;
            BuildUI();
        }

        void Update()
        {
            HandleTouches();
            if (Input.touchCount == 0) HandleMouse();

            // look
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            cam.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            // walk: joystick, or keyboard in the editor
            var input = moveVector;
            var keys = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (keys.sqrMagnitude > 0f) input = Vector2.ClampMagnitude(keys, 1f);
            float speed = walkSpeed * (Input.GetKey(KeyCode.LeftShift) ? 2f : 1f);
            cc.SimpleMove(transform.rotation * new Vector3(input.x, 0f, input.y) * speed);
            var flat = new Vector2(transform.position.x, transform.position.z);
            if (flat.magnitude > maxRadius)
            {
                flat = flat.normalized * maxRadius;
                cc.enabled = false;
                transform.position = new Vector3(flat.x, transform.position.y, flat.y);
                cc.enabled = true;
            }

            UpdateFps();
        }

        // ------------------------------------------------------------------ input

        bool InStickZone(Vector2 p) => p.x < Screen.width * 0.5f && p.y < Screen.height * 0.45f;

        void OnDestroy() { batches.Dispose(); triangles.Dispose(); }

        void ToggleScope()
        {
            scoped = !scoped;
            cam.fieldOfView = scoped ? scopeFov : normalFov;
            scopeImage.color = new Color(1f, 1f, 1f, scoped ? 0.75f : 0.3f);
        }

        void PointerDown(int id, Vector2 p)
        {
            if (scopeImage && Vector2.Distance(p, scopeImage.rectTransform.position) < scopeImage.rectTransform.sizeDelta.x * 0.6f)
            {
                ToggleScope();
                return;
            }
            if (p.x < Screen.width * 0.7f && p.y > Screen.height * 0.86f)
            {
                QualitySettings.SetQualityLevel((QualitySettings.GetQualityLevel() + 1) % QualitySettings.names.Length);
                return;
            }
            if (moveId == int.MinValue && InStickZone(p))
            {
                moveId = id; moveOrigin = p; moveVector = Vector2.zero;
                stickBase.position = p; stickKnob.position = p;
                baseImage.color = new Color(1f, 1f, 1f, 0.45f); knobImage.color = new Color(1f, 1f, 1f, 0.8f);
            }
            else if (lookId == int.MinValue) { lookId = id; lastLookPos = p; }
        }

        void PointerMove(int id, Vector2 p)
        {
            if (id == moveId)
            {
                var d = Vector2.ClampMagnitude(p - moveOrigin, stickRadius);
                moveVector = d / stickRadius;
                stickKnob.position = moveOrigin + d;
            }
            else if (id == lookId)
            {
                var d = p - lastLookPos;
                lastLookPos = p;
                float k = lookSpeed / Screen.height * (cam.fieldOfView / normalFov);    // finer through the scope
                yaw += d.x * k;
                pitch = Mathf.Clamp(pitch - d.y * k, -maxPitch, maxPitch);
            }
        }

        void PointerUp(int id)
        {
            if (id == moveId)
            {
                moveId = int.MinValue; moveVector = Vector2.zero;
                stickBase.position = stickHome; stickKnob.position = stickHome;
                baseImage.color = new Color(1f, 1f, 1f, 0.2f); knobImage.color = new Color(1f, 1f, 1f, 0.35f);
            }
            if (id == lookId) lookId = int.MinValue;
        }

        void HandleTouches()
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                switch (t.phase)
                {
                    case TouchPhase.Began: PointerDown(t.fingerId, t.position); break;
                    case TouchPhase.Moved:
                    case TouchPhase.Stationary: PointerMove(t.fingerId, t.position); break;
                    default: PointerUp(t.fingerId); break;
                }
            }
        }

        void HandleMouse()
        {
            Vector2 p = Input.mousePosition;
            if (Input.GetMouseButtonDown(1)) ToggleScope();
            if (Input.GetMouseButtonDown(0)) PointerDown(MouseId, p);
            else if (Input.GetMouseButton(0)) PointerMove(MouseId, p);
            else if (Input.GetMouseButtonUp(0)) PointerUp(MouseId);
        }

        // ------------------------------------------------------------------ UI

        void BuildUI()
        {
            var canvasGo = new GameObject("TestControllerUI", typeof(Canvas));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            float unit = Mathf.Min(Screen.width, Screen.height);
            stickRadius = unit * 0.13f;
            var safe = Screen.safeArea;
            stickHome = new Vector2(safe.xMin + unit * 0.24f, safe.yMin + unit * 0.3f);
            var circle = CircleSprite();
            baseImage = MakeImage(canvasGo.transform, "StickBase", circle, stickRadius * 2f, new Color(1f, 1f, 1f, 0.2f));
            knobImage = MakeImage(canvasGo.transform, "StickKnob", circle, stickRadius * 0.9f, new Color(1f, 1f, 1f, 0.35f));
            stickBase = baseImage.rectTransform; stickKnob = knobImage.rectTransform;
            stickBase.position = stickHome; stickKnob.position = stickHome;
            scopeImage = MakeImage(canvasGo.transform, "Scope", circle, unit * 0.2f, new Color(1f, 1f, 1f, 0.3f));
            scopeImage.rectTransform.position = new Vector2(safe.xMax - unit * 0.17f, safe.yMin + unit * 0.3f);

            var textGo = new GameObject("Fps", typeof(RectTransform), typeof(Text), typeof(Shadow));
            textGo.transform.SetParent(canvasGo.transform, false);
            fpsText = textGo.GetComponent<Text>();
            fpsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            fpsText.fontSize = Mathf.RoundToInt(unit * 0.045f);
            fpsText.color = Color.white;
            fpsText.raycastTarget = false;
            fpsText.horizontalOverflow = HorizontalWrapMode.Overflow;
            var rt = fpsText.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(unit, unit * 0.2f);
            rt.anchoredPosition = new Vector2(safe.xMin + unit * 0.03f, -(Screen.height - safe.yMax) - unit * 0.03f);
            textGo.GetComponent<Shadow>().effectDistance = new Vector2(2f, -2f);
        }

        static Image MakeImage(Transform parent, string name, Sprite sprite, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite; img.color = color; img.raycastTarget = false;
            img.rectTransform.sizeDelta = new Vector2(size, size);
            return img;
        }

        static Sprite CircleSprite()
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = new Vector2(x - n / 2 + 0.5f, y - n / 2 + 0.5f).magnitude / (n / 2);
                    float a = Mathf.Clamp01((1f - d) * n / 4f);                 // anti-aliased edge
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px); tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        void UpdateFps()
        {
            float dt = Time.unscaledDeltaTime;
            fpsTimer += dt; fpsFrames++;
            worstFrame = Mathf.Max(worstFrame, dt);
            if (fpsTimer < 0.5f) return;
            float fps = fpsFrames / fpsTimer;
            fpsText.text = $"{fps:0} FPS  {1000f / fps:0.0} ms  (worst {worstFrame * 1000f:0} ms)\n{QualitySettings.names[QualitySettings.GetQualityLevel()]}  {Screen.width}x{Screen.height}";
            fpsTimer = 0f; fpsFrames = 0; worstFrame = 0f;
        }
    }
}
