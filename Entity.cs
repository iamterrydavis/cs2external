using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace cs2external
{
    public class Entity
    {
        public IntPtr pawnAdress { get; set; }

        public IntPtr controllerAddress { get; set; }
        public Vector3 origin { get; set; }
        public Vector3 view { get; set; }
        public int health { get; set; }
        public int team { get; set; }
        public uint lifeState { get; set; }
        public float distance { get; set; }
        public Vector3 head { get; set; }
        public float fov { get; set; }
        public bool isSpotted { get; set; }
        public bool isVisible { get; set; }
        public Vector3 velocity { get; set; }
        public string name { get; set; } = "Unknown";
        public string weaponName { get; set; } = "None";
        public Vector2 head2D { get; set; } = Vector2.Zero;
        public Vector2 origin2D { get; set; } = Vector2.Zero;
        public Dictionary<int, Vector2> bone2D { get; set; } = new Dictionary<int, Vector2>();
    }
}
