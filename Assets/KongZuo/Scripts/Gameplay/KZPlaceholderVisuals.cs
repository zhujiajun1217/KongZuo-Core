using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KongZuo
{
    /// <summary>
    /// Replaces the most visible greybox renderers with lightweight coloured primitive models.
    /// Gameplay objects, colliders and interactable components are deliberately left untouched.
    /// </summary>
    public static class KZPlaceholderVisuals
    {
        private const string VisualRootName = "KZ_低模上色外观";
        private static readonly Dictionary<string, Material> Materials = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Polish(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Polish(scene);

        private static void Polish(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            foreach (GameObject root in scene.GetRootGameObjects())
                PolishRecursive(root.transform);
        }

        private static void PolishRecursive(Transform item)
        {
            string name = item.name;
            if (item.Find(VisualRootName) == null)
            {
                // The integrated opera-house prefab already supplies its own desks. Decorating
                // the old greybox desk creates a second, floating table over the authored one.
                if (name.Contains("电话")) BuildPhone(item);
                else if (name.Contains("清点表") || name.Contains("登记表") ||
                         name.Contains("节目单") || name.Contains("宣传册") ||
                         name.Contains("旧合照")) BuildPaper(item, name.Contains("旧合照"));
                else if (name.Contains("海报")) BuildPoster(item);
                else if (name.Contains("票根")) BuildTicket(item);
                else if (name.Contains("座次示意图")) BuildPuzzleBoard(item);
                else if (name.Contains("钥匙")) BuildKeys(item);
                // Use exact gameplay names. Some pieces of the imported architecture also contain
                // words such as “人影”; decorating those made a permanent figure appear on stage.
                else if (name == "一排七座的人影_异常" || name.StartsWith("1992观众_") ||
                         name.Contains("沈芸_演出占位")) BuildFigure(item, name);
            }

            // Work on a snapshot because builders add children during traversal.
            int count = item.childCount;
            for (int i = 0; i < count; i++)
            {
                Transform child = item.GetChild(i);
                if (child.name != VisualRootName) PolishRecursive(child);
            }
        }

        private static Transform Begin(Transform target)
        {
            Renderer own = target.GetComponent<Renderer>();
            if (own != null) own.enabled = false;
            GameObject holder = new GameObject(VisualRootName);
            holder.transform.SetPositionAndRotation(target.position, target.rotation);
            holder.transform.SetParent(target, true);
            return holder.transform;
        }

        private static void BuildPhone(Transform target)
        {
            Transform v = Begin(target);
            Part(v, "电话底座", PrimitiveType.Cube, new Vector3(0, .08f, 0), new Vector3(.58f, .16f, .46f), "黑胶木", new Color(.055f, .06f, .065f));
            Part(v, "拨号盘", PrimitiveType.Cylinder, new Vector3(0, .18f, 0), new Vector3(.28f, .035f, .28f), "旧铜", new Color(.52f, .38f, .18f), new Vector3(90, 0, 0));
            Part(v, "听筒中段", PrimitiveType.Cube, new Vector3(0, .31f, 0), new Vector3(.48f, .09f, .11f), "黑胶木", new Color(.04f, .045f, .05f));
            Part(v, "听筒左", PrimitiveType.Sphere, new Vector3(-.25f, .31f, 0), new Vector3(.16f, .13f, .15f), "黑胶木", new Color(.04f, .045f, .05f));
            Part(v, "听筒右", PrimitiveType.Sphere, new Vector3(.25f, .31f, 0), new Vector3(.16f, .13f, .15f), "黑胶木", new Color(.04f, .045f, .05f));
        }

        private static void BuildPaper(Transform target, bool photo)
        {
            Transform v = Begin(target);
            Color paper = photo ? new Color(.46f, .38f, .28f) : new Color(.82f, .72f, .50f);
            Part(v, "旧纸", PrimitiveType.Cube, Vector3.zero, new Vector3(.82f, .035f, .62f), photo ? "旧照片" : "旧纸", paper);
            for (int i = -2; i <= 2; i++)
                Part(v, "印刷行", PrimitiveType.Cube, new Vector3(0, .021f, i * .09f), new Vector3(.58f, .006f, .018f), "墨", new Color(.18f, .12f, .08f));
            if (photo)
                Part(v, "照片人物", PrimitiveType.Cube, new Vector3(0, .025f, .04f), new Vector3(.38f, .008f, .30f), "褪色照片", new Color(.25f, .22f, .18f));
        }

        private static void BuildPoster(Transform target)
        {
            Transform v = Begin(target);
            Part(v, "木框", PrimitiveType.Cube, Vector3.zero, new Vector3(.92f, 1.32f, .06f), "暗木", new Color(.16f, .065f, .025f));
            Part(v, "海报纸", PrimitiveType.Cube, new Vector3(0, 0, -.04f), new Vector3(.78f, 1.17f, .025f), "朱红海报", new Color(.55f, .08f, .055f));
            Part(v, "海报标题", PrimitiveType.Cube, new Vector3(0, .34f, -.06f), new Vector3(.55f, .09f, .012f), "旧金", new Color(.85f, .62f, .23f));
        }

        private static void BuildTicket(Transform target)
        {
            Transform v = Begin(target);
            Part(v, "票根主体", PrimitiveType.Cube, Vector3.zero, new Vector3(.66f, .035f, .42f), "褪红票纸", new Color(.64f, .19f, .14f));
            Part(v, "缺角", PrimitiveType.Cube, new Vector3(.27f, .025f, .16f), new Vector3(.15f, .014f, .12f), "旧纸", new Color(.82f, .72f, .50f), new Vector3(0, 24, 0));
            Part(v, "票号", PrimitiveType.Cube, new Vector3(-.08f, .025f, 0), new Vector3(.30f, .008f, .025f), "墨", new Color(.17f, .08f, .06f));
        }

        private static void BuildPuzzleBoard(Transform target)
        {
            Transform v = Begin(target);
            Part(v, "木板", PrimitiveType.Cube, Vector3.zero, new Vector3(.92f, .10f, .70f), "深木", new Color(.25f, .10f, .045f));
            for (int i = -1; i <= 1; i++)
            {
                Part(v, "座位格", PrimitiveType.Cube, new Vector3(i * .25f, .07f, 0), new Vector3(.20f, .035f, .48f), "旧纸", new Color(.82f, .72f, .50f));
                Part(v, "铜钉", PrimitiveType.Sphere, new Vector3(i * .25f, .105f, -.25f), new Vector3(.035f, .025f, .035f), "铜", new Color(.72f, .46f, .12f));
            }
        }

        private static void BuildKeys(Transform target)
        {
            Transform v = Begin(target);
            Part(v, "钥匙环", PrimitiveType.Cylinder, Vector3.zero, new Vector3(.18f, .04f, .18f), "铜", new Color(.76f, .48f, .12f));
            for (int i = 0; i < 3; i++)
            {
                float angle = -28f + i * 28f;
                Part(v, "钥匙杆", PrimitiveType.Cube, new Vector3((i - 1) * .08f, 0, .15f), new Vector3(.045f, .035f, .34f), "铜", new Color(.76f, .48f, .12f), new Vector3(0, angle, 0));
                Part(v, "钥匙齿", PrimitiveType.Cube, new Vector3((i - 1) * .11f, 0, .31f), new Vector3(.10f, .035f, .07f), "铜", new Color(.76f, .48f, .12f), new Vector3(0, angle, 0));
            }
        }

        private static void BuildFigure(Transform target, string name)
        {
            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            Transform v = Begin(target);
            bool actress = name.Contains("沈芸");
            Color cloth = actress ? new Color(.56f, .055f, .10f) : new Color(.10f, .13f, .17f);
            Part(v, "躯干", PrimitiveType.Capsule, new Vector3(0, 1.02f, 0), new Vector3(.46f, .65f, .34f), actress ? "戏服朱红" : "观众深蓝", cloth);
            Part(v, "头", PrimitiveType.Sphere, new Vector3(0, 1.77f, 0), new Vector3(.38f, .40f, .38f), "肤色", new Color(.68f, .50f, .39f));
            Part(v, "头发", PrimitiveType.Sphere, new Vector3(0, 1.89f, .01f), new Vector3(.40f, .25f, .40f), "发色", new Color(.035f, .025f, .02f));
            Part(v, "左臂", PrimitiveType.Capsule, new Vector3(-.33f, 1.08f, 0), new Vector3(.14f, .52f, .14f), actress ? "戏服朱红" : "观众深蓝", cloth, new Vector3(0, 0, -10));
            Part(v, "右臂", PrimitiveType.Capsule, new Vector3(.33f, 1.08f, 0), new Vector3(.14f, .52f, .14f), actress ? "戏服朱红" : "观众深蓝", cloth, new Vector3(0, 0, 10));
            if (actress)
                Part(v, "水袖", PrimitiveType.Cube, new Vector3(0, .62f, 0), new Vector3(.78f, .72f, .34f), "戏服朱红", cloth);
        }

        private static GameObject Part(Transform parent, string name, PrimitiveType type,
            Vector3 position, Vector3 scale, string materialName, Color color, Vector3 euler = default)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localEulerAngles = euler;
            part.transform.localScale = scale;
            Object.Destroy(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = Material(materialName, color);
            return part;
        }

        private static Material Material(string name, Color color)
        {
            if (Materials.TryGetValue(name, out Material found) && found != null) return found;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material material = new Material(shader) { name = "KZ_" + name };
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", name.Contains("铜") ? .55f : .18f);
            Materials[name] = material;
            return material;
        }
    }
}
