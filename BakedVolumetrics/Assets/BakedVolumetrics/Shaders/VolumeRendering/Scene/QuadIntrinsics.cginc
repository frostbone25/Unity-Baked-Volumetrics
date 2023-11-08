// SPDX-License-Identifier: MIT
// Author: pema99

// This file contains functions that simulate Quad and Wave Intrinsics without access to either.
// For more information on those, see: https://github.com/Microsoft/DirectXShaderCompiler/wiki/Wave-Intrinsics

// To use the functions, you must call SETUP_QUAD_INTRINSICS(pos) at the start of your fragment shader,
// where 'pos' is the pixel position, ie. the fragment input variable with the SV_Position semantic.
// Note that some functions will require SM 5.0, ie. #pragma target 5.0.

// The file is a bit difficult to read, so here is a quick reference of all the functions it provides:
//
// Basic getters:
//   uint QuadGetLaneID() - Get the ID of the current lane (0-3), from top left to bottom right.
//   uint2 QuadGetLanePosition() - Get the position of the current lane (0,0 - 1,1), from top left to bottom right.
//
// Shuffles and broadcasts:
//   <float_type> QuadReadAcrossX(<float_type> value) - Read the value of the lane opposite this one on the X axis.
//   <float_type> QuadReadAcrossY(<float_type> value) - Read the value of the lane opposite this one on the Y axis.
//   <float_type> QuadReadAcrossDiagonal(<float_type> value) - Read the value of the lane opposite this one on the diagonal.
//   <float_type> QuadReadLaneAt(<float_type> value, uint2 quadLaneID) - Read the value of the lane at the given position.
//   <float_type> QuadReadLaneAt(<float_type> value, uint quadLaneID) - Read the value of the lane with the given ID.
//   void QuadReadAll(<float_type> value, out <float_type> topLeft, out <float_type> topRight, out <float_type> bottomLeft, out <float_type> bottomRight) - Read the value of all lanes.
//
// Reductions:
//   bool QuadAny(bool expr) - Check if any lane evaluate the expression to true.
//   bool QuadAll(bool expr) - Check if all lanes evaluate the expression to true.
//   <float_type> QuadSum(<float_type> value) - Sum the values on all lanes.
//   <float_type> QuadProduct(<float_type> value) - Multiply the values on all lanes.
//   <float_type> QuadMin(<float_type> value) - Find the minimum value on all lanes.
//   <float_type> QuadMax(<float_type> value) - Find the maximum value on all lanes.
//   <integer_type> QuadBitAnd(<integer_type> value) - Bitwise AND the values on all lanes.
//   <integer_type> QuadBitOr(<integer_type> value) - Bitwise OR the values on all lanes.
//   <integer_type> QuadBitXor(<integer_type> value) - Bitwise XOR the values on all lanes.
//   uint4 QuadBallot(bool expr) - Create a bitmask of which lanes evaluate the expression to true.
//   uint QuadCountBits(bool expr) - Count the number of lanes that evaluate the expression to true.
//
// Scans:
//   <float_type> QuadPrefixSum(<float_type> value) - Sum the values on all lanes up to and exlcuding this one.
//   <float_type> QuadPrefixProduct(<float_type> value) - Multiply the values on all lanes up to and exlcuding this one.
//   uint QuadPrefixCountBits(bool expr) - Count the number of lanes that evaluate the expression to true up to and excluding this one.

#ifndef QUAD_INTRINSICS
#define QUAD_INTRINSICS

// Setup functions
static uint2 GLOBAL_QUAD_INDEX = uint2(0, 0);

#define SETUP_QUAD_INTRINSICS(SV_Position) \
    GLOBAL_QUAD_INDEX = (uint2)(SV_Position).xy & 1;

// ID getters
uint QuadGetLaneID()
{
    return ((GLOBAL_QUAD_INDEX.y * 1) << 1) + (GLOBAL_QUAD_INDEX.x & 1);
}

uint2 QuadGetLanePosition()
{
    return GLOBAL_QUAD_INDEX;
}

// Helper functions
#define GENERIC_QUAD_FLOAT_HELPERS(T) \
float QUAD_ADD_HELPER(T a, T b)       \
{                                     \
    return a + b;                     \
}                                     \

// NOTE: The reason we don't implement these for all types is because the HLSL compiler selects
// overloads based on the size of the type - thus, we can't have any instances that take parameters
// of the same size, as the overloads will overlap.
GENERIC_QUAD_FLOAT_HELPERS(float);
GENERIC_QUAD_FLOAT_HELPERS(float2);
GENERIC_QUAD_FLOAT_HELPERS(float3);
GENERIC_QUAD_FLOAT_HELPERS(float4);
GENERIC_QUAD_FLOAT_HELPERS(float3x3);
GENERIC_QUAD_FLOAT_HELPERS(float4x4);

#define GENERIC_QUAD_INTEGER_HELPERS(T) \
T QUAD_BITAND_HELPER(T a, T b)          \
{                                       \
    return a & b;                       \
}                                       \
                                        \
T QUAD_BITOR_HELPER(T a, T b)           \
{                                       \
    return a | b;                       \
}                                       \
                                        \
T QUAD_BITXOR_HELPER(T a, T b)          \
{                                       \
    return a ^ b;                       \
}

GENERIC_QUAD_INTEGER_HELPERS(uint);
GENERIC_QUAD_INTEGER_HELPERS(uint2);
GENERIC_QUAD_INTEGER_HELPERS(uint3);
GENERIC_QUAD_INTEGER_HELPERS(uint4);
GENERIC_QUAD_FLOAT_HELPERS(uint3x3);
GENERIC_QUAD_FLOAT_HELPERS(uint4x4);

uint QUAD_COUNT_BITS_HELPER(uint a, uint b)
{
    return a + b;
}

// Generic intrinsics
#define GENERIC_QUAD_REDUCTION(T, Name, OP)                                                     \
T Name(T value)                                                                                 \
{                                                                                               \
    T topLeft, topRight, bottomLeft, bottomRight;                                               \
    QuadReadAll(value, topLeft, topRight, bottomLeft, bottomRight);                             \
    return OP(OP(OP(topLeft, topRight), bottomLeft), bottomRight);                              \
}

#define GENERIC_QUAD_SCAN(T, Name, OP)                                                          \
T Name(T value)                                                                                 \
{                                                                                               \
    T topLeft, topRight, bottomLeft, bottomRight;                                               \
    QuadReadAll(value, topLeft, topRight, bottomLeft, bottomRight);                             \
    T allValues[4] = { topLeft, topRight, bottomLeft, bottomRight };                            \
                                                                                                \
    T prefix = 0;                                                                               \
    for (int i = 0; i < QuadGetLaneID(); i++)                                                   \
    {                                                                                           \
        prefix = OP(prefix, allValues[i]);                                                      \
    }                                                                                           \
    return prefix;                                                                              \
}

#define GENERIC_QUAD_FLOAT_INTRINSICS(T)                                                        \
T QuadReadAcrossX(T value)                                                                      \
{                                                                                               \
    T diff = ddx_fine(value);                                                                   \
    float sign = GLOBAL_QUAD_INDEX.x == 0 ? 1 : -1;                                             \
    return (sign * diff) + value;                                                               \
}                                                                                               \
                                                                                                \
T QuadReadAcrossY(T value)                                                                      \
{                                                                                               \
    T diff = ddy_fine(value);                                                                   \
    float sign = GLOBAL_QUAD_INDEX.y == 0 ? 1 : -1;                                             \
    return (sign * diff) + value;                                                               \
}                                                                                               \
                                                                                                \
T QuadReadAcrossDiagonal(T value)                                                               \
{                                                                                               \
    T oppositeX = QuadReadAcrossX(value);                                                       \
    T oppositeDiagonal = QuadReadAcrossY(oppositeX);                                            \
    return oppositeDiagonal;                                                                    \
}                                                                                               \
                                                                                                \
T QuadReadLaneAt(T value, uint2 quadLaneID)                                                     \
{                                                                                               \
    uint2 offset = 0;                                                                           \
    bool2 correct = quadLaneID == GLOBAL_QUAD_INDEX;                                            \
    if (all(correct))                                                                           \
    {                                                                                           \
        return value;                                                                           \
    }                                                                                           \
    else if (correct.x)                                                                         \
    {                                                                                           \
        return QuadReadAcrossY(value);                                                          \
    }                                                                                           \
    else if (correct.y)                                                                         \
    {                                                                                           \
        return QuadReadAcrossX(value);                                                          \
    }                                                                                           \
    else                                                                                        \
    {                                                                                           \
        return QuadReadAcrossDiagonal(value);                                                   \
    }                                                                                           \
}                                                                                               \
                                                                                                \
T QuadReadLaneAt(T value, uint quadLaneID)                                                      \
{                                                                                               \
    uint2 offset = 0;                                                                           \
    return QuadReadLaneAt(value, uint2(quadLaneID & 1, (quadLaneID & 2) >> 1));                 \
}                                                                                               \
                                                                                                \
void QuadReadAll(T value, out T topLeft, out T topRight, out T bottomLeft, out T bottomRight)   \
{                                                                                               \
    topLeft = QuadReadLaneAt(value, uint2(0, 0));                                               \
    topRight = QuadReadLaneAt(value, uint2(1, 0));                                              \
    bottomLeft = QuadReadLaneAt(value, uint2(0, 1));                                            \
    bottomRight = QuadReadLaneAt(value, uint2(1, 1));                                           \
}                                                                                               \
                                                                                                \
GENERIC_QUAD_REDUCTION(T, QuadSum, QUAD_ADD_HELPER)                                             \
GENERIC_QUAD_REDUCTION(T, QuadProduct, mul)                                                     \
GENERIC_QUAD_REDUCTION(T, QuadMin, min)                                                         \
GENERIC_QUAD_REDUCTION(T, QuadMax, max)                                                         \
                                                                                                \
GENERIC_QUAD_SCAN(T, QuadPrefixSum, QUAD_ADD_HELPER)                                            \
GENERIC_QUAD_SCAN(T, QuadPrefixProduct, mul)                                                    \

GENERIC_QUAD_FLOAT_INTRINSICS(float);
GENERIC_QUAD_FLOAT_INTRINSICS(float2);
GENERIC_QUAD_FLOAT_INTRINSICS(float3);
GENERIC_QUAD_FLOAT_INTRINSICS(float4);
GENERIC_QUAD_FLOAT_INTRINSICS(float3x3);
GENERIC_QUAD_FLOAT_INTRINSICS(float4x4);

// Generic, integer-specific intrincs
#define GENERIC_QUAD_INTEGER_INTRINSICS(T)                  \
GENERIC_QUAD_REDUCTION(T, QuadBitAnd, QUAD_BITAND_HELPER)   \
GENERIC_QUAD_REDUCTION(T, QuadBitOr, QUAD_BITOR_HELPER)     \
GENERIC_QUAD_REDUCTION(T, QuadBitXor, QUAD_BITXOR_HELPER)

GENERIC_QUAD_INTEGER_INTRINSICS(uint);
GENERIC_QUAD_INTEGER_INTRINSICS(uint2);
GENERIC_QUAD_INTEGER_INTRINSICS(uint3);
GENERIC_QUAD_INTEGER_INTRINSICS(uint4);
GENERIC_QUAD_INTEGER_INTRINSICS(uint3x3);
GENERIC_QUAD_INTEGER_INTRINSICS(uint4x4);

// Monomorphic intrinsics
bool QuadAny(bool expr)
{
    return QuadReadLaneAt(expr, 0) || QuadReadLaneAt(expr, 1) || QuadReadLaneAt(expr, 2) || QuadReadLaneAt(expr, 3);
}

bool QuadAll(bool expr)
{
    return QuadReadLaneAt(expr, 0) && QuadReadLaneAt(expr, 1) && QuadReadLaneAt(expr, 2) && QuadReadLaneAt(expr, 3);
}

uint4 QuadBallot(bool expr)
{
    uint4 result;
    result.x = QuadReadLaneAt(expr ? 1 : 0, 0);
    result.y = QuadReadLaneAt(expr ? 1 : 0, 1);
    result.z = QuadReadLaneAt(expr ? 1 : 0, 2);
    result.w = QuadReadLaneAt(expr ? 1 : 0, 3);
    return result;
}

uint QuadCountBits(bool expr)
{
    uint4 ballot = QuadBallot(expr);
    return ballot.x + ballot.y + ballot.z + ballot.w;
}

GENERIC_QUAD_SCAN(uint, QuadPrefixCountBitsHelper, QUAD_COUNT_BITS_HELPER);
uint QuadPrefixCountBits(bool expr)
{
    return QuadPrefixCountBitsHelper(expr ? 1 : 0);
}

// Clean up helper macros
#undef GENERIC_QUAD_INTEGER_HELPERS
#undef GENERIC_QUAD_FLOAT_HELPERS
#undef GENERIC_QUAD_REDUCTION
#undef GENERIC_QUAD_SCAN
#undef GENERIC_QUAD_FLOAT_INTRINSICS
#undef GENERIC_QUAD_INTEGER_INTRINSICS

#endif