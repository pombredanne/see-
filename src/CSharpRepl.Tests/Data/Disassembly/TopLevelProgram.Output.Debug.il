﻿.class private auto ansi '<Module>'
{
} // end of class <Module>

.class private auto ansi beforefieldinit Program
    extends [System.Runtime]System.Object
{
    .custom instance void [System.Runtime]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = (
        01 00 00 00
    )
    // Methods
    .method private hidebysig static
        void '<Main>$' (
            string[] args
        ) cil managed
    {
        // Method begins at RVA 0x2050
        // Code size 14 (0xe)
        .maxstack 2
        .entrypoint
        .locals init (
            [0] int32,
            [1] int32
        )

        IL_0000: ldc.i4.5
        IL_0001: stloc.0
        IL_0002: ldc.i4.3
        IL_0003: stloc.1
        IL_0004: ldloc.0
        IL_0005: ldloc.1
        IL_0006: add
        IL_0007: call void [System.Console]System.Console::WriteLine(int32)
        IL_000c: nop
        IL_000d: ret
    } // end of method Program::'<Main>$'

    .method public hidebysig specialname rtspecialname
        instance void .ctor () cil managed
    {
        // Method begins at RVA 0x206a
        // Code size 8 (0x8)
        .maxstack 8

        IL_0000: ldarg.0
        IL_0001: call instance void [System.Runtime]System.Object::.ctor()
        IL_0006: nop
        IL_0007: ret
    } // end of method Program::.ctor

} // end of class Program

// Disassembled in Debug Mode. Press Ctrl+F9 to disassemble in Release Mode.
// Compiling code as Console Application (with top-level statements): succeeded.