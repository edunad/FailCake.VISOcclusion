#region

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;

#endregion

namespace FailCake.VIS
{
    // EXAMPLE ROOM ------------------
    [RequireComponent(typeof(entity_vis_room), typeof(BoxCollider))]
    public class entity_vis_test : MonoBehaviour
    {
        [Header("Settings")]
        public List<Renderer> ignore = new List<Renderer>();

        #region PRIVATE

        private entity_vis_room _room;
        private BoxCollider _collider;
        private bool _visible;

        // LISTS ---------
        private List<Renderer> _renderers = new List<Renderer>();
        private List<Light> _lights = new List<Light>();
        private List<DecalProjector> _decals = new List<DecalProjector>();
        // ---------------

        #endregion

        public void Awake() {
            this._collider = this.GetComponent<BoxCollider>();
            if (!this._collider) throw new UnityException("entity_vis_test requires a BoxCollider component on the same GameObject!");
            this._collider.isTrigger = true;

            this._room = this.GetComponent<entity_vis_room>();
            if (!this._room) throw new UnityException("entity_vis_test requires an entity_vis_room component on the same GameObject!");

            this._renderers = this.GetComponentsInChildren<Renderer>(false).Where(m => !this.ignore.Contains(m) && m.enabled).ToList();
            this._lights = this.GetComponentsInChildren<Light>(true).Where(l => l.shadows != LightShadows.None).ToList();
            this._decals = this.GetComponentsInChildren<DecalProjector>(true).ToList();

            // SETUP ----
            this._room.IsInside = pos => this._collider?.bounds.Contains(pos) == true;
            this._room.OnVisibilityChanged += visible => {
                if (this._visible == visible) return;
                this._visible = visible;

                foreach (Renderer r in this._renderers)
                    if (r)
                        r.enabled = visible;

                foreach (Light l in this._lights)
                    if (l)
                        l.shadows = visible ? LightShadows.Soft : LightShadows.None;

                foreach (DecalProjector d in this._decals)
                    if (d)
                        d.enabled = visible;
            };
            // ----
        }
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