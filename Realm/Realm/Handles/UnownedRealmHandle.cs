﻿////////////////////////////////////////////////////////////////////////////
//
// Copyright 2016 Realm Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
////////////////////////////////////////////////////////////////////////////

using System;

namespace Realms
{
    // this class represents a Realm that isn't owned by .net - like the realms
    // in the object store migration callback
    internal class UnownedRealmHandle : SharedRealmHandle
    {
        public UnownedRealmHandle(IntPtr handle) : base(handle)
        {
        }

        public override bool OwnsNativeRealm => false;

        public override void AddChild(RealmHandle handle)
        {
            base.AddChild(handle);

            // The unowned realm handle needs to keep track of all children,
            // not just the ones that are forcing ownership.
            if (!handle.ForceRootOwnership)
            {
                _weakChildren.Add(new(handle));
            }
        }

        protected override void Unbind()
        {
            // do nothing - we don't own this, so we don't need to clean up
        }
    }
}
