#region

using UnityEngine;
using UnityEngine.Scripting;

#endregion

namespace FailCake.VIS
{
    [Preserve]
    public class entity_vis_portal : MonoBehaviour
    {
        [Header("Settings")]
        public bool open = true;

        public Vector3 size = Vector3.one;

        #region PRIVATE

        protected PortalStatus _status;

        #endregion

        public void Awake() {
            if (!VISController.Instance) throw new UnityException("Missing VIS Controller");
            VISController.Instance?.RegisterPortal(this);
        }

        public void OnDestroy() {
            VISController.Instance?.UnregisterPortal(this);
        }

        #region STATUS

        public void SetStatus(PortalStatus status) { this._status = status; }

        public PortalStatus GetPortalStatus() { return this._status; }

        public virtual bool IsOpen() { return this.open; }

        #endregion

        #if UNITY_EDITOR
        public void OnValidate() {
            if (Application.isPlaying) return;
            this.gameObject.isStatic = false;
        }
        #endif
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