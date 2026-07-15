// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Buffers.Binary;
using Xunit;
using Xunit.Abstractions;

namespace SharpEmu.ShaderCompiler.Metal.Tests;

/// <summary>
/// Tier 2 and 3: the emitted MSL must compile with the OS runtime Metal compiler, and
/// the executable fixture must produce bit-exact results on the GPU, including EXEC
/// masking. These tests no-op (with a note) on hosts without a Metal device so the suite
/// stays green on Windows/Linux CI; the golden tier still runs everywhere.
/// </summary>
public sealed class MetalRuntimeTests(ITestOutputHelper output)
{
    [Fact]
    public void AllFixturesCompileWithTheRuntimeMetalCompiler()
    {
        if (!MetalNative.IsAvailable)
        {
            output.WriteLine("No Metal device on this host; compile validation skipped.");
            return;
        }

        foreach (var fixture in Gen5ComputeFixtures.All)
        {
            var shader = Gen5ComputeFixtures.CompileOrThrow(fixture);
            Assert.True(
                MetalNative.TryCompileLibrary(shader.Source, out _, out var error),
                $"[{fixture.Name}] Metal rejected the emitted MSL: {error}\n{shader.Source}");
        }
    }

    [Fact]
    public void ExecStoreProgramExecutesWithExecMasking()
    {
        if (!MetalNative.IsAvailable)
        {
            output.WriteLine("No Metal device on this host; execution test skipped.");
            return;
        }

        var shader = Gen5ComputeFixtures.CompileOrThrow(Gen5ComputeFixtures.ExecStore);
        Assert.True(
            MetalNative.TryCompileLibrary(shader.Source, out var library, out var compileError),
            compileError);

        // Sentinel-filled buffer: any dword the program does not store must survive.
        const uint Sentinel = 0xDEADBEEFu;
        var buffer = new byte[64];
        for (var offset = 0; offset < buffer.Length; offset += sizeof(uint))
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset), Sentinel);
        }

        Assert.True(
            MetalNative.TryExecuteSingleThread(library, shader.EntryPoint, buffer, out var result, out var runError),
            runError);

        // Reference results computed with the same semantics the program encodes.
        var fmac = BitConverter.SingleToUInt32Bits(MathF.FusedMultiplyAdd(1.5f, 2.25f, 10.0f));
        var mulHiSigned = (uint)(((long)0x7FFFFFFF * 0x00010003) >> 32);
        var mulLoSigned = unchecked(0x7FFFFFFFu * 0x00010003u);
        var movBits = BitConverter.SingleToUInt32Bits(1.5f);

        Assert.Equal(fmac, ReadDword(result, 0));
        Assert.Equal(mulHiSigned, ReadDword(result, 4));
        Assert.Equal(mulLoSigned, ReadDword(result, 8));
        Assert.Equal(Sentinel, ReadDword(result, 12)); // EXEC=0: the store must not land.
        Assert.Equal(movBits, ReadDword(result, 16));
        for (var offset = 20; offset < result.Length; offset += sizeof(uint))
        {
            Assert.Equal(Sentinel, ReadDword(result, offset));
        }
    }

    private static uint ReadDword(byte[] buffer, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(offset));
}
