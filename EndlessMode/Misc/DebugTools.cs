using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EndlessMode.Misc
{
    class DebugTools
    {
        public static void PrintObjectHierarchy()
        {
            Logger.Success("BEGINNING TREE");
            foreach (GameObject obj in Object.FindObjectsOfType(typeof(GameObject)))
            {
                if (obj.transform.parent == null)
                {
                    Logger.Warning($"NEW TREE ROOT: {obj.name}");
                    Traverse(obj);
                }
            }
        }

        public static void Traverse(GameObject obj, string history = null)
        {
            Logger.Info($"BRANCH: {history}/{obj.name}");
            foreach (Transform child in obj.transform)
            {
                Traverse(child.gameObject, history + $"/{obj.name}");
            }
        }

        public static void LogComponents(Transform t, string prefix = "=", bool includeScipts = false)
        {
            Console.WriteLine(prefix + ">" + t.name + ": x = " + t.localScale.x);

            if (includeScipts)
            {
                foreach (var comp in t.GetComponents<MonoBehaviour>())
                {
                    Console.WriteLine(prefix + "-->" + comp.GetType() + ": x = " + comp.transform.localScale.x);
                }
            }

            foreach (Transform child in t)
            {
                LogComponents(child, prefix + "=", includeScipts);
            }
        }
    }
}
