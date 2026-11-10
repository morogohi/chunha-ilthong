// 노치·다이내믹 아일랜드·홈 인디케이터를 피해 UI를 배치한다. Screen.safeArea 가 바뀌면(회전·분할 화면) 다시 맞춘다.
using UnityEngine;

namespace Chunha.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        Rect last;
        Vector2Int lastSize;

        public static RectTransform Create(Canvas canvas)
        {
            var rt = UIKit.Rect("SafeArea", canvas.transform).Stretch();
            rt.gameObject.AddComponent<SafeArea>().Apply();
            return rt;
        }

        void Update() => Apply();

        public void Apply()
        {
            var sa = Screen.safeArea;
            var size = new Vector2Int(Screen.width, Screen.height);
            if (sa == last && size == lastSize) return;
            last = sa; lastSize = size;
            if (size.x <= 0 || size.y <= 0) return;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(sa.xMin / size.x, sa.yMin / size.y);
            rt.anchorMax = new Vector2(sa.xMax / size.x, sa.yMax / size.y);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // 캔버스는 폭 1080 기준으로 맞춰진다(CanvasScaler match=0). 생성 직후에도 맞도록 scaleFactor 대신 직접 환산한다.
        static float ToCanvas(float px) => Screen.width > 0 ? px * UIKit.W / Screen.width : 0;

        /// <summary>화면 위쪽 안전 여백(캔버스 단위). 전체 화면 오버레이에서 띠를 내릴 때 쓴다.</summary>
        public static float TopInset() => ToCanvas(Screen.height - Screen.safeArea.yMax);

        /// <summary>화면 아래쪽 안전 여백(캔버스 단위). 홈 인디케이터 높이.</summary>
        public static float BottomInset() => ToCanvas(Screen.safeArea.yMin);
    }
}
