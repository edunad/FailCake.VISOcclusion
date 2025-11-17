#region

using UnityEngine;
using UnityEngine.Scripting;
#if UNITY_EDITOR
using UnityEditor;
#endif

#endregion

namespace FailCake.VIS
{
    [Preserve]
    public class entity_vis_portal_3d : entity_vis_portal
    {
        [Header("Rooms"), SerializeField]
        protected entity_vis_room Room;

        public new void Awake() {
            base.Awake();
            this.Room?.AddPortal(this);
        }

        public new void OnDestroy() {
            base.OnDestroy();
            this.Room?.RemovePortal(this);
        }

        public override bool IsOpen() { return this.Room && base.IsOpen(); }

        #region ROOMS

        public void SetRoom(entity_vis_room currentRoom) {
            if (!Application.isPlaying)
            {
                this.Room = currentRoom;
                return;
            }

            if (this.Room) this.Room.RemovePortal(this);
            this.Room = currentRoom;
            this.Room?.AddPortal(this);
        }

        public entity_vis_room GetRoom() { return this.Room; }

        #endregion

        #region PRIVATE

        #if UNITY_EDITOR
        private void OnDrawGizmos() {
            // CUBE -----------
            Gizmos.matrix = this.transform.localToWorldMatrix;

            Gizmos.color = this.open ? Color.cyan : Color.red;
            Gizmos.DrawWireCube(Vector3.zero, this.size);
            Gizmos.matrix = Matrix4x4.identity;
            // ------------------------------

            // LINK ---------------
            if (!this.Room) return;
            Vector3 start = this.transform.position;
            Vector3 end = this.Room.transform.position;
            Vector3 offset = Vector3.up * Vector3.Distance(start, end) * 0.25f;

            Handles.DrawBezier(start, end, start + offset, end + offset, Color.green, null, 2f);
            // ------------------------------
        }
        #endif

        #endregion
    }
}


/*# MIT License Copyright (c) 2025 FailCake

# Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the
# "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish,
# distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to
# the following conditions:
#
# The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
#
# THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
# MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR
# ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
# SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.*/