#region

using UnityEditor;
using UnityEngine;
using UnityEngine.Scripting;

#endregion

namespace FailCake.VIS
{
    [Preserve]
    public class entity_vis_portal_2d : entity_vis_portal
    {
        [Header("Rooms"), SerializeField]
        protected entity_vis_room RoomA;

        [SerializeField]
        protected entity_vis_room RoomB;

        public new void Awake() {
            base.Awake();

            this.RoomA?.AddPortal(this);
            this.RoomB?.AddPortal(this);
        }

        public new void OnDestroy() {
            base.OnDestroy();

            this.RoomA?.RemovePortal(this);
            this.RoomB?.RemovePortal(this);
        }

        #region ROOMS

        public void SetRoomA(entity_vis_room currentRoom) {
            if (!Application.isPlaying)
            {
                this.RoomA = currentRoom;
                return;
            }

            if (this.RoomA) this.RoomA.RemovePortal(this);
            this.RoomA = currentRoom;
            this.RoomA?.AddPortal(this);
        }

        public void SetRoomB(entity_vis_room currentRoom) {
            if (!Application.isPlaying)
            {
                this.RoomB = currentRoom;
                return;
            }

            if (this.RoomB) this.RoomB.RemovePortal(this);
            this.RoomB = currentRoom;
            this.RoomB?.AddPortal(this);
        }

        public entity_vis_room GetRoomA() { return this.RoomA; }
        public entity_vis_room GetRoomB() { return this.RoomB; }

        #endregion

        public override bool IsOpen() { return (this.RoomA || this.RoomB) && base.IsOpen(); }

        #region PRIVATE

        #if UNITY_EDITOR
        private void OnDrawGizmos() {
            // Draw the portal rectangle
            Gizmos.matrix = this.transform.localToWorldMatrix;

            Gizmos.color = this.open ? Color.cyan : Color.red;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(this.size.x, this.size.y, 0.01f));
            // -------------------------------

            // Draw direction ---
            Vector3 arrowEnd = Vector3.forward * 0.5f;

            this.DrawArrow(Vector3.zero, arrowEnd, Color.blue);
            this.DrawArrow(Vector3.zero, -arrowEnd, Color.red);
            // ------------------

            Gizmos.matrix = Matrix4x4.identity;

            // Draw link ----
            if (this.RoomA)
            {
                Vector3 start = this.transform.position;
                Vector3 end = this.RoomA.transform.position;
                Vector3 offset = Vector3.up * Vector3.Distance(start, end) * 0.25f;

                Handles.DrawBezier(start, end, start + offset, end + offset, Color.blue, null, 2f);
            }

            if (this.RoomB)
            {
                Vector3 start = this.transform.position;
                Vector3 end = this.RoomB.transform.position;
                Vector3 offset = Vector3.up * Vector3.Distance(start, end) * 0.25f;

                Handles.DrawBezier(start, end, start + offset, end + offset, Color.red, null, 2f);
            }

            Handles.Label(this.transform.position + this.transform.forward * 0.6f + Vector3.up * 0.2f, "A");
            Handles.Label(this.transform.position - this.transform.forward * 0.6f + Vector3.up * 0.2f, "B");
            // --------------
        }

        private void DrawArrow(Vector3 from, Vector3 to, Color color) {
            Gizmos.color = color;
            Gizmos.DrawLine(from, to);

            Vector3 direction = (to - from).normalized;
            Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 150, 0) * Vector3.forward;
            Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, -150, 0) * Vector3.forward;

            Gizmos.DrawLine(to, to + right * 0.1f);
            Gizmos.DrawLine(to, to + left * 0.1f);
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