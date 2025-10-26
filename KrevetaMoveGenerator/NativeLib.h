#pragma once

#include <cstdint>

uint64_t Factorial(int n) {
    if (n <= 1)
        return 1;

    return n * Factorial(n - 1);
}