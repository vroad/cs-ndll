﻿using System;
using System.Runtime.InteropServices;

namespace cs
{
    class CSAbstract : IDisposable
    {
        [UnmanagedFunctionPointerAttribute(CallingConvention.Cdecl)]
        internal delegate IntPtr FinalizerDelegate(IntPtr arg1);

        internal IntPtr Pointer { get; private set; }
        internal int Kind { get; private set; }
        private FinalizerDelegate finalizer;
        internal FinalizerDelegate Finalizer
        {
            get
            {
                return finalizer;
            }
            set
            {
                if (finalizer != null)
                    throw new InvalidOperationException("Finalizer is already set");

                finalizer = value;
            }
        }

        internal CSAbstract(int kind, IntPtr ptr)
        {
            this.Pointer = ptr;
            this.Kind = kind;
            this.finalizer = null;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (this.Finalizer != null)
            {
                GCHandle handle = GCHandle.Alloc(this, GCHandleType.Normal);
                finalizer(GCHandle.ToIntPtr(handle));
                this.finalizer = null;
            }
        }

        ~CSAbstract()
        {
            Dispose(false);
        }
    }
}