// 지도 조작: 한 손가락 끌기 = 이동, 두 손가락 = 확대·축소, 짧게 누르기 = 도시 선택.
// 기존 Input Manager를 쓴다 (Player Settings › Active Input Handling = Both 또는 Input Manager).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Chunha
{
    public partial class GameRoot
    {
        Vector3 dragStartScreen, rigStart;
        bool dragging, pressOnUi;
        float lastPinch;
        bool wasPinching;
        static readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        const float TapSlop = 18f;

        void Update()
        {
            if (S == null || busy || inBattle || cam == null) return;
            if (Input.touchCount >= 2) { Pinch(); wasPinching = true; return; }
            lastPinch = 0;

            bool down, held, up; Vector3 pos;
            if (Input.touchCount == 1)
            {
                var t = Input.GetTouch(0);
                pos = t.position;
                down = t.phase == TouchPhase.Began;
                held = t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary;
                up = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
                if (down) pressOnUi = UiAt(pos);
                if (wasPinching)
                {
                    // 두 손가락에서 한 손가락으로 바뀔 때 끌기 기준점을 새로 잡아 카메라가 튀지 않게 한다
                    wasPinching = false;
                    dragStartScreen = pos; rigStart = rig.position; dragging = true; pressOnUi = false;
                }
            }
            else
            {
                pos = Input.mousePosition;
                down = Input.GetMouseButtonDown(0);
                held = Input.GetMouseButton(0);
                up = Input.GetMouseButtonUp(0);
                if (down) pressOnUi = UiAt(pos);
                float wheel = Input.mouseScrollDelta.y;
                if (Mathf.Abs(wheel) > 0.01f && !UiAt(pos)) Zoom(-wheel * 0.6f);
            }
            if (sheetOpen) return;

            if (down && !pressOnUi) { dragStartScreen = pos; rigStart = rig.position; dragging = false; StopFocus(); }
            if (held && !pressOnUi)
            {
                if (!dragging && (pos - dragStartScreen).magnitude > TapSlop) dragging = true;
                if (dragging)
                {
                    var a = cam.ScreenToWorldPoint(dragStartScreen); var b = cam.ScreenToWorldPoint(pos);
                    rig.position = ClampToMap(rigStart - (b - a));
                }
            }
            if (up && !pressOnUi && !dragging)
            {
                var w = cam.ScreenToWorldPoint(pos); w.z = 0;
                var n = map.NodeAt(w);
                if (n != null) Select(n.id);
            }
            if (up) { dragging = false; pressOnUi = false; }
        }

        /// <summary>그 화면 좌표에 UI가 있는지 직접 레이캐스트한다 (EventSystem 갱신 순서에 의존하지 않는다)</summary>
        bool UiAt(Vector2 p)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            var data = new PointerEventData(es) { position = p };
            uiHits.Clear();
            es.RaycastAll(data, uiHits);
            return uiHits.Count > 0;
        }

        void Pinch()
        {
            var a = Input.GetTouch(0); var b = Input.GetTouch(1);
            float d = Vector2.Distance(a.position, b.position);
            if (lastPinch > 0) Zoom((lastPinch - d) * 0.01f);
            lastPinch = d;
            dragging = true;
        }

        void Zoom(float delta)
        {
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize + delta, 4.5f, 9.5f);
            rig.position = ClampToMap(rig.position);
        }
    }
}
