using System;
using System.Reflection;
using System.Runtime.InteropServices;
using static cs.CFFICSLoader;

namespace cs
{
    public class NDLLFunction : IDisposable
    {
        public static bool Initialized { get; private set; }

        private IntPtr module;
        private Delegate func;
        private int numArgs;
        private static CFFILoaderDelegate loaderDelegate;
        private static GCHandle pinnedLoaderFunc;

        internal static Type ArrayType { get; private set; }
        internal static Type ReflectType { get; private set; }
        internal static Type FunctionType { get; private set; }
        internal static Type ObjectType { get; private set; }
        internal static FieldInfo ArrayLengthInfo { get; private set; }
        internal static FieldInfo NativeArrayInfo { get; private set; }
        internal static MethodInfo ReflectSetFieldInfo { get; private set; }
        internal static MethodInfo ReflectFieldInfo { get; private set; }
        internal static MethodInfo FunctionInvokeInfo { get; private set; }
        internal static ConstructorInfo ObjectCtorInfo { get; private set; }

        [UnmanagedFunctionPointerAttribute(CallingConvention.Cdecl)]
        private delegate IntPtr NDLLFunctionDelegate();
        [UnmanagedFunctionPointerAttribute(CallingConvention.Cdecl)]
        private delegate void HxSetLoaderDelegate(IntPtr loader);
        [UnmanagedFunctionPointerAttribute(CallingConvention.Cdecl)]
        private delegate IntPtr CallMultDelegate(IntPtr args);
        [UnmanagedFunctionPointerAttribute(CallingConvention.Cdecl)]
        private delegate IntPtr Call0Delegate();
        [UnmanagedFunctionPointerAttribute(CallingConvention.Cdecl)]
        private delegate IntPtr Call1Delegate(IntPtr arg1);
        [UnmanagedFunctionPointerAttribute(CallingConvention.Cdecl)]
        private delegate IntPtr Call2Delegate(IntPtr arg1, IntPtr arg2);
        [UnmanagedFunctionPointerAttribute(CallingConvention.Cdecl)]
        private delegate IntPtr Call3Delegate(IntPtr arg1, IntPtr arg2, IntPtr arg3);
        [UnmanagedFunctionPointerAttribute(CallingConvention.Cdecl)]
        private delegate IntPtr Call4Delegate(IntPtr arg1, IntPtr arg2, IntPtr arg3, IntPtr arg4);
        [UnmanagedFunctionPointerAttribute(CallingConvention.Cdecl)]
        private delegate IntPtr Call5Delegate(IntPtr arg1, IntPtr arg2, IntPtr arg3, IntPtr arg4, IntPtr arg5);

        NDLLFunction(IntPtr module, Delegate func, int numArgs)
        {
            this.module = module;
            this.func = func;
            this.numArgs = numArgs;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (module != IntPtr.Zero)
            {
                NativeMethods.FreeLibraryWrap(module);
                module = IntPtr.Zero;
            }
        }

        ~NDLLFunction()
        {
            Dispose(false);
        }

        public static void Initialize(Type arrayType, Type reflectType, Type functionType, Type hxObjectType)
        {
            Initialized = true;
            NDLLFunction.ArrayType = arrayType;
            NDLLFunction.ReflectType = reflectType;
            NDLLFunction.FunctionType = functionType;
            NDLLFunction.ObjectType = hxObjectType;

            ArrayLengthInfo = arrayType.GetField("length");
            if (ArrayLengthInfo == null)
                throw new ArgumentException("Couldn't find field 'length' for Array");
            else if (Type.GetTypeCode(ArrayLengthInfo.FieldType) != TypeCode.Int32)
                throw new ArgumentException("Array.length is not Int32");

            NativeArrayInfo = arrayType.GetField("__a");
            if (NativeArrayInfo == null)
                throw new ArgumentException("Couldn't find field '__a' for Array");
            else if (!NativeArrayInfo.FieldType.IsArray)
                throw new ArgumentException("Array.__a is not Native Array");

            ReflectSetFieldInfo = reflectType.GetMethod("setField");
            if (ReflectSetFieldInfo == null)
                throw new ArgumentException("Couldn't find static method 'setField' for Reflect");
            ReflectFieldInfo = reflectType.GetMethod("field");
            if (ReflectFieldInfo == null)
                throw new ArgumentException("Couldn't find static method 'field' for Reflect");

            FunctionInvokeInfo = functionType.GetMethod("__hx_invokeDynamic");
            if (FunctionInvokeInfo == null)
                throw new ArgumentException("Couldn't find static method '__hx_invokeDynamic' for Function");

            Type[] types = { };
            ObjectCtorInfo = hxObjectType.GetConstructor(types);
            if (ObjectCtorInfo == null)
                throw new ArgumentException("Couldn't find constructor for DynamicObject");
        }

        public static NDLLFunction Load(String lib, String name, int numArgs)
        {
            if (!Initialized)
                throw new InvalidOperationException("NDLLFunction is not initialized");
            if (numArgs < -1 || numArgs > 5)
                throw new ArgumentOutOfRangeException("Invalid numArgs: " + numArgs);

            IntPtr module = IntPtr.Zero;
            try
            {
                module = NativeMethods.LoadLibraryWrap(lib);
                if (module == IntPtr.Zero)
                {
                    return null;
                }

                String funcName;
                if (numArgs != -1)
                    funcName = String.Format("{0}__{1}", name, numArgs);
                else
                    funcName = String.Format("{0}__MULT", name);

                IntPtr funcPtr = NativeMethods.GetProcAddressWrap(module, funcName);
                if (funcPtr == IntPtr.Zero)
                {
                    //NativeMethods.FreeLibraryWrap(module);
                    return null;
                }
                NDLLFunctionDelegate func = (NDLLFunctionDelegate)Marshal.GetDelegateForFunctionPointer(funcPtr, typeof(NDLLFunctionDelegate));
                Delegate cfunc = null;
                switch (numArgs)
                {
                    case -1:
                        cfunc = Marshal.GetDelegateForFunctionPointer(func(), typeof(CallMultDelegate));
                        break;
                    case 0:
                        cfunc = Marshal.GetDelegateForFunctionPointer(func(), typeof(Call0Delegate));
                        break;
                    case 1:
                        cfunc = Marshal.GetDelegateForFunctionPointer(func(), typeof(Call1Delegate));
                        break;
                    case 2:
                        cfunc = Marshal.GetDelegateForFunctionPointer(func(), typeof(Call2Delegate));
                        break;
                    case 3:
                        cfunc = Marshal.GetDelegateForFunctionPointer(func(), typeof(Call3Delegate));
                        break;
                    case 4:
                        cfunc = Marshal.GetDelegateForFunctionPointer(func(), typeof(Call4Delegate));
                        break;
                    case 5:
                        cfunc = Marshal.GetDelegateForFunctionPointer(func(), typeof(Call5Delegate));
                        break;
                }

                IntPtr dll_hx_set_loader_ptr = NativeMethods.GetProcAddressWrap(module, "hx_set_loader");
                if (dll_hx_set_loader_ptr == IntPtr.Zero)
                {
                    //NativeMethods.FreeLibraryWrap(module);
                    return null;
                }
                HxSetLoaderDelegate dll_hx_set_loader = (HxSetLoaderDelegate)Marshal.GetDelegateForFunctionPointer(dll_hx_set_loader_ptr, typeof(HxSetLoaderDelegate));
                IntPtr callbackPtr;
                if (loaderDelegate == null)
                {
                    loaderDelegate = new CFFILoaderDelegate(CFFICSLoader.Load);
                    callbackPtr = Marshal.GetFunctionPointerForDelegate(loaderDelegate);
                    pinnedLoaderFunc = GCHandle.Alloc(callbackPtr, GCHandleType.Pinned);
                }
                else
                {
                    callbackPtr = (IntPtr)pinnedLoaderFunc.Target;
                }

                dll_hx_set_loader(callbackPtr);

                NDLLFunction ndllFunc = new NDLLFunction(module, cfunc, numArgs);
                module = IntPtr.Zero;
                return ndllFunc;
            }
            finally
            {
                if (module != IntPtr.Zero)
                    NativeMethods.FreeLibraryWrap(module);
            }
        }

        public object CallMult(Object args)
        {
            if (numArgs != -1)
                throw new InvalidOperationException();
            if (!args.GetType().Equals(ArrayType))
                throw new ArgumentException();

            CSHandleScope scope = CSHandleScope.Create();
            object[] arrayArgs = (object[])NDLLFunction.NativeArrayInfo.GetValue(args);
            GCHandle[] handles = new GCHandle[arrayArgs.Length];
            for (int i = 0; i < arrayArgs.Length; ++i)
                handles[i] = GCHandle.Alloc(arrayArgs[i]);
            IntPtr[] pointers = new IntPtr[arrayArgs.Length];
            for (int i = 0; i < arrayArgs.Length; ++i)
                pointers[i] = GCHandle.ToIntPtr(handles[i]);
            GCHandle pinnedArray = GCHandle.Alloc(pointers, GCHandleType.Pinned);

            CallMultDelegate cfunc = (CallMultDelegate)func;
            object result = HandleUtils.GetObjectFromIntPtr(cfunc(pinnedArray.AddrOfPinnedObject()));
            scope.Destroy();
            for (int i = 0; i < arrayArgs.Length; ++i)
                handles[i].Free();
            pinnedArray.Free();
		    return result;
        }

        public object Call0()
        {
            if (numArgs != 0)
                throw new InvalidOperationException();

            CSHandleScope scope = CSHandleScope.Create();
            Call0Delegate cfunc = (Call0Delegate)func;
            object result = HandleUtils.GetObjectFromIntPtr(cfunc());
            scope.Destroy();
            return result;
        }

        public object Call1(object arg1)
        {
            if (numArgs != 1)
                throw new InvalidOperationException();

            CSHandleScope scope = CSHandleScope.Create();
            Call1Delegate cfunc = (Call1Delegate)func;
            GCHandle gch1 = GCHandle.Alloc(arg1);
            object result = HandleUtils.GetObjectFromIntPtr(cfunc(GCHandle.ToIntPtr(gch1)));
            scope.Destroy();
            gch1.Free();
            return result;
        }

        public object Call2(object arg1, object arg2)
        {
            if (numArgs != 2)
                throw new InvalidOperationException();

            CSHandleScope scope = CSHandleScope.Create();
            Call2Delegate cfunc = (Call2Delegate)func;
            GCHandle gch1 = GCHandle.Alloc(arg1);
            GCHandle gch2 = GCHandle.Alloc(arg2);
            object result = HandleUtils.GetObjectFromIntPtr(cfunc(GCHandle.ToIntPtr(gch1), GCHandle.ToIntPtr(gch2)));
            scope.Destroy();
            gch1.Free();
            gch2.Free();
            return result;
        }

        public object Call3(object arg1, object arg2, object arg3)
        {
            if (numArgs != 3)
                throw new InvalidOperationException();

            CSHandleScope scope = CSHandleScope.Create();
            Call3Delegate cfunc = (Call3Delegate)func;
            GCHandle gch1 = GCHandle.Alloc(arg1);
            GCHandle gch2 = GCHandle.Alloc(arg2);
            GCHandle gch3 = GCHandle.Alloc(arg3);
            object result = HandleUtils.GetObjectFromIntPtr(cfunc(GCHandle.ToIntPtr(gch1), GCHandle.ToIntPtr(gch2), GCHandle.ToIntPtr(gch3)));
            scope.Destroy();
            gch1.Free();
            gch2.Free();
            gch3.Free();
            return result;
        }

        public object Call4(Object arg1, Object arg2, Object arg3, Object arg4)
        {
            if (numArgs != 4)
                throw new InvalidOperationException();

            CSHandleScope scope = CSHandleScope.Create();
            Call4Delegate cfunc = (Call4Delegate)func;
            GCHandle gch1 = GCHandle.Alloc(arg1);
            GCHandle gch2 = GCHandle.Alloc(arg2);
            GCHandle gch3 = GCHandle.Alloc(arg3);
            GCHandle gch4 = GCHandle.Alloc(arg4);
            object result = HandleUtils.GetObjectFromIntPtr(cfunc(GCHandle.ToIntPtr(gch1), GCHandle.ToIntPtr(gch2), GCHandle.ToIntPtr(gch3), GCHandle.ToIntPtr(gch4)));
            scope.Destroy();
            gch1.Free();
            gch2.Free();
            gch3.Free();
            gch4.Free();
            return result;
        }

        public Object Call5(Object arg1, Object arg2, Object arg3, Object arg4, Object arg5)
        {
            if (numArgs != 5)
                throw new InvalidOperationException();

            CSHandleScope scope = CSHandleScope.Create();
            Call5Delegate cfunc = (Call5Delegate)func;
            GCHandle gch1 = GCHandle.Alloc(arg1);
            GCHandle gch2 = GCHandle.Alloc(arg2);
            GCHandle gch3 = GCHandle.Alloc(arg3);
            GCHandle gch4 = GCHandle.Alloc(arg4);
            GCHandle gch5 = GCHandle.Alloc(arg5);
            object result = HandleUtils.GetObjectFromIntPtr(cfunc(GCHandle.ToIntPtr(gch1),
                GCHandle.ToIntPtr(gch2), GCHandle.ToIntPtr(gch3), GCHandle.ToIntPtr(gch4), GCHandle.ToIntPtr(gch5)));
            scope.Destroy();
            gch1.Free();
            gch2.Free();
            gch3.Free();
            gch4.Free();
            gch5.Free();
            return result;
        }
    }
}
