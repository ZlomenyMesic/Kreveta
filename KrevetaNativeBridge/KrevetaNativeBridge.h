#pragma once

#include "../KrevetaMoveGenerator/NativeLib.h"

using namespace System;

namespace NativeBridge {

    public ref class NativeMovegen {

    public:
        static uint64_t Factorial(int n) {
            return ::Factorial(n);
        }
    };
}