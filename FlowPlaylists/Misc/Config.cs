using FlowPlaylists.SimpleJSON;
using System;
using System.IO;
using UnityEngine;

namespace FlowPlaylists
{
    class Config
    {
        public static bool Enabled { get; set; }
        public static Vector3 Position { get; set; }
        public static Vector3 Rotation { get; set; }
        public static Vector3 Size { get; set; }
        public static Vector3 Scale { get; set; }

        private static string ConfigLocation = $"{Environment.CurrentDirectory}/UserData/FlowPlaylists.txt";

        public static void LoadConfig()
        {
            if (File.Exists(ConfigLocation))
            {
                JSONNode node = JSON.Parse(File.ReadAllText(ConfigLocation));
                Enabled = bool.Parse(node["Enabled"].Value);
                Position = Vector3FromNode("Position", node);
                Rotation = Vector3FromNode("Rotation", node);
                Size = Vector3FromNode("Size", node);
                Scale = Vector3FromNode("Scale", node);
            }
            else
            {
                Enabled = false;
                Position = new Vector3(0, 5f, 7.5f);
                Rotation = new Vector3(0, 0, 0);
                Size = new Vector2(500, 250);
                Scale = new Vector3(0.01f, 0.01f, 0.01f);
                SaveConfig();
            }
        }

        private static Vector3 Vector3FromNode(string vectorName, JSONNode node)
        {
            float x = float.Parse(node[$"{vectorName}-X"].Value);
            float y = float.Parse(node[$"{vectorName}-Y"].Value);
            float z = float.Parse(node[$"{vectorName}-Z"].Value);
            return new Vector3(x, y, z);
        }

        private static void NodeFromVector3(string vectorName, Vector3 vector, JSONNode node)
        {
            node[$"{vectorName}-X"] = vector.x;
            node[$"{vectorName}-Y"] = vector.y;
            node[$"{vectorName}-Z"] = vector.z;
        }

        public static void SaveConfig()
        {
            JSONNode node = new JSONObject();
            node["Enabled"] = Enabled;
            NodeFromVector3("Position", Position, node);
            NodeFromVector3("Rotation", Rotation, node);
            NodeFromVector3("Size", Size, node);
            NodeFromVector3("Scale", Scale, node);
            File.WriteAllText(ConfigLocation, node.ToString());
        }
    }
}
