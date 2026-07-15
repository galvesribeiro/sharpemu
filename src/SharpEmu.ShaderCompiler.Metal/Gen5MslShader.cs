// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.Libs.Agc;

namespace SharpEmu.ShaderCompiler.Metal;

/// <summary>
/// A compiled Metal shader: MSL source text plus the binding layout the dispatcher
/// needs. Metal fixes the threadgroup size at dispatch time rather than in the shader,
/// so the requested size is carried here.
/// </summary>
internal sealed record Gen5MslShader(
    string Source,
    string EntryPoint,
    IReadOnlyList<Gen5GlobalMemoryBinding> GlobalMemoryBindings,
    uint ThreadgroupSizeX,
    uint ThreadgroupSizeY,
    uint ThreadgroupSizeZ);
