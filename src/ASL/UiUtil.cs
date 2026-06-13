using TMPro;
using UnityEngine;

namespace ASL
{
    internal static class UiUtil
    {
        /// <summary>
        /// Sets a cloned button's TMP label, first removing any extra script on the label object
        /// (e.g. a localizer) that would otherwise overwrite the text back to the original — which
        /// is why a relabelled clone was still showing "Settings".
        /// </summary>
        public static void SetLabel(GameObject root, string text)
        {
            if (root == null) return;
            var tmp = root.GetComponentInChildren<TMP_Text>(true);
            if (tmp == null) return;

            var labelGo = tmp.gameObject;
            var monos = labelGo.GetComponents<MonoBehaviour>();
            for (int i = 0; i < monos.Length; i++)
            {
                var m = monos[i];
                if (m == null) continue;
                if (m.TryCast<TMP_Text>() == null) UnityEngine.Object.Destroy(m);  // drop localizer / overrides
            }

            tmp.text = text;
        }
    }
}
