namespace Intel8080Tools;

public enum Pin : byte
{
    /// <summary>
    ///     Address bus 10
    /// </summary>
    /// <remark>Output</remark>
    A10 = 1,

    /// <summary>
    ///     Ground
    /// </summary>
    /// <remark>—</remark>
    GND = 2,

    /// <summary>
    ///     Bidirectional data bus. The processor also transiently sets here the "processor state", providing information about
    ///     what the processor is currently doing:<br />
    ///     D0 reading interrupt command. In response to the interrupt signal, the processor is reading and executing a single
    ///     arbitrary command with this flag raised. Normally the supporting chips provide the subroutine call command (CALL or
    ///     RST), transferring control to the interrupt handling code.<br />
    ///     D1 reading (low level means writing)<br />
    ///     D2 accessing stack (probably a separate stack memory space was initially planned)<br />
    ///     D3 doing nothing, has been halted by the HLT instruction<br />
    ///     D4 writing data to an output port<br />
    ///     D5 reading the first byte of an executable instruction<br />
    ///     D6 reading data from an input port<br />
    ///     D7 reading data from memory
    /// </summary>
    /// <remark>Bidirectional</remark>
    D4 = 3,

    /// <summary>
    ///     Bidirectional data bus. The processor also transiently sets here the "processor state", providing information about
    ///     what the processor is currently doing:<br />
    ///     D0 reading interrupt command. In response to the interrupt signal, the processor is reading and executing a single
    ///     arbitrary command with this flag raised. Normally the supporting chips provide the subroutine call command (CALL or
    ///     RST), transferring control to the interrupt handling code.<br />
    ///     D1 reading (low level means writing)<br />
    ///     D2 accessing stack (probably a separate stack memory space was initially planned)<br />
    ///     D3 doing nothing, has been halted by the HLT instruction<br />
    ///     D4 writing data to an output port<br />
    ///     D5 reading the first byte of an executable instruction<br />
    ///     D6 reading data from an input port<br />
    ///     D7 reading data from memory
    /// </summary>
    /// <remark>Bidirectional</remark>
    D5 = 4,

    /// <summary>
    ///     Bidirectional data bus. The processor also transiently sets here the "processor state", providing information about
    ///     what the processor is currently doing:<br />
    ///     D0 reading interrupt command. In response to the interrupt signal, the processor is reading and executing a single
    ///     arbitrary command with this flag raised. Normally the supporting chips provide the subroutine call command (CALL or
    ///     RST), transferring control to the interrupt handling code.<br />
    ///     D1 reading (low level means writing)<br />
    ///     D2 accessing stack (probably a separate stack memory space was initially planned)<br />
    ///     D3 doing nothing, has been halted by the HLT instruction<br />
    ///     D4 writing data to an output port<br />
    ///     D5 reading the first byte of an executable instruction<br />
    ///     D6 reading data from an input port<br />
    ///     D7 reading data from memory
    /// </summary>
    /// <remark>Bidirectional</remark>
    D6 = 5,

    /// <summary>
    ///     Bidirectional data bus. The processor also transiently sets here the "processor state", providing information about
    ///     what the processor is currently doing:<br />
    ///     D0 reading interrupt command. In response to the interrupt signal, the processor is reading and executing a single
    ///     arbitrary command with this flag raised. Normally the supporting chips provide the subroutine call command (CALL or
    ///     RST), transferring control to the interrupt handling code.<br />
    ///     D1 reading (low level means writing)<br />
    ///     D2 accessing stack (probably a separate stack memory space was initially planned)<br />
    ///     D3 doing nothing, has been halted by the HLT instruction<br />
    ///     D4 writing data to an output port<br />
    ///     D5 reading the first byte of an executable instruction<br />
    ///     D6 reading data from an input port<br />
    ///     D7 reading data from memory
    /// </summary>
    /// <remark>Bidirectional</remark>
    D7 = 6,

    /// <summary>
    ///     Bidirectional data bus. The processor also transiently sets here the "processor state", providing information about
    ///     what the processor is currently doing:<br />
    ///     D0 reading interrupt command. In response to the interrupt signal, the processor is reading and executing a single
    ///     arbitrary command with this flag raised. Normally the supporting chips provide the subroutine call command (CALL or
    ///     RST), transferring control to the interrupt handling code.<br />
    ///     D1 reading (low level means writing)<br />
    ///     D2 accessing stack (probably a separate stack memory space was initially planned)<br />
    ///     D3 doing nothing, has been halted by the HLT instruction<br />
    ///     D4 writing data to an output port<br />
    ///     D5 reading the first byte of an executable instruction<br />
    ///     D6 reading data from an input port<br />
    ///     D7 reading data from memory
    /// </summary>
    /// <remark>Bidirectional</remark>
    D3 = 7,

    /// <summary>
    ///     Bidirectional data bus. The processor also transiently sets here the "processor state", providing information about
    ///     what the processor is currently doing:<br />
    ///     D0 reading interrupt command. In response to the interrupt signal, the processor is reading and executing a single
    ///     arbitrary command with this flag raised. Normally the supporting chips provide the subroutine call command (CALL or
    ///     RST), transferring control to the interrupt handling code.<br />
    ///     D1 reading (low level means writing)<br />
    ///     D2 accessing stack (probably a separate stack memory space was initially planned)<br />
    ///     D3 doing nothing, has been halted by the HLT instruction<br />
    ///     D4 writing data to an output port<br />
    ///     D5 reading the first byte of an executable instruction<br />
    ///     D6 reading data from an input port<br />
    ///     D7 reading data from memory
    /// </summary>
    /// <remark>Bidirectional</remark>
    D2 = 8,

    /// <summary>
    ///     Bidirectional data bus. The processor also transiently sets here the "processor state", providing information about
    ///     what the processor is currently doing:<br />
    ///     D0 reading interrupt command. In response to the interrupt signal, the processor is reading and executing a single
    ///     arbitrary command with this flag raised. Normally the supporting chips provide the subroutine call command (CALL or
    ///     RST), transferring control to the interrupt handling code.<br />
    ///     D1 reading (low level means writing)<br />
    ///     D2 accessing stack (probably a separate stack memory space was initially planned)<br />
    ///     D3 doing nothing, has been halted by the HLT instruction<br />
    ///     D4 writing data to an output port<br />
    ///     D5 reading the first byte of an executable instruction<br />
    ///     D6 reading data from an input port<br />
    ///     D7 reading data from memory
    /// </summary>
    /// <remark>Bidirectional</remark>
    D1 = 9,

    /// <summary>
    ///     Bidirectional data bus. The processor also transiently sets here the "processor state", providing information about
    ///     what the processor is currently doing:<br />
    ///     D0 reading interrupt command. In response to the interrupt signal, the processor is reading and executing a single
    ///     arbitrary command with this flag raised. Normally the supporting chips provide the subroutine call command (CALL or
    ///     RST), transferring control to the interrupt handling code.<br />
    ///     D1 reading (low level means writing)<br />
    ///     D2 accessing stack (probably a separate stack memory space was initially planned)<br />
    ///     D3 doing nothing, has been halted by the HLT instruction<br />
    ///     D4 writing data to an output port<br />
    ///     D5 reading the first byte of an executable instruction<br />
    ///     D6 reading data from an input port<br />
    ///     D7 reading data from memory
    /// </summary>
    /// <remark>Bidirectional</remark>
    D0 = 10,

    /// <summary>
    ///     The −5 V power supply. This must be the first power source connected and the last disconnected, otherwise the
    ///     processor will be damaged.
    /// </summary>
    /// <remark>—</remark>
    N5_V = 11,

    /// <summary>
    ///     Reset. This active low signal forces execution of commands located at address 0000. The content of other processor
    ///     registers is not modified.
    /// </summary>
    /// <remark>Input</remark>
    RESET = 12,

    /// <summary>
    ///     Direct memory access request. The processor is requested to switch the data and address bus to the high impedance
    ///     ("disconnected") state.
    /// </summary>
    /// <remark>Input</remark>
    HOLD = 13,

    /// <summary>
    ///     Interrupt request
    /// </summary>
    /// <remark>Input</remark>
    INT = 14,

    /// <summary>
    ///     The second phase of the clock generator signal
    /// </summary>
    /// <remark>Input</remark>
    PHASE2 = 15,

    /// <summary>
    ///     The processor has two commands for setting 0 or 1 level on this pin. The pin normally is supposed to be used for
    ///     interrupt control. However, in simple computers it was sometimes used as a single bit output port for various
    ///     purposes.
    /// </summary>
    /// <remark>Output</remark>
    INTE = 16,

    /// <summary>
    ///     Read (the processor reads from memory or input port)
    /// </summary>
    /// <remark>Output</remark>
    DBIN = 17,

    /// <summary>
    ///     Write (the processor writes to memory or output port). This is an active low output.
    /// </summary>
    /// <remark>Output</remark>
    WR = 18,

    /// <summary>
    ///     Active level indicates that the processor has put the "state word" on the data bus. The various bits of this state
    ///     word provide added information to support the separate address and memory spaces, interrupts, and direct memory
    ///     access. This signal is required to pass through additional logic before it can be used to write the processor state
    ///     word from the data bus into some external register, e.g., 8238 Archived September 18, 2023, at the Wayback
    ///     Machine-System Controller and Bus Driver.
    /// </summary>
    /// <remark>Output</remark>
    SYNC = 19,

    /// <summary>
    ///     The + 5 V power supply
    /// </summary>
    /// <remark>—</remark>
    P5_V = 20,

    /// <summary>
    ///     Direct memory access confirmation. The processor switches data and address pins into the high impedance state,
    ///     allowing another device to manipulate the bus
    /// </summary>
    /// <remark>Output</remark>
    HLDA = 21,

    /// <summary>
    ///     The first phase of the clock generator signal
    /// </summary>
    /// <remark>Input</remark>
    PHASE1 = 22,

    /// <summary>
    ///     Wait. With this signal it is possible to suspend the processor's work. It is also used to support the
    ///     hardware-based step-by step debugging mode.
    /// </summary>
    /// <remark>Input</remark>
    READY = 23,

    /// <summary>
    ///     Wait (indicates that the processor is in the waiting state)
    /// </summary>
    /// <remark>Output</remark>
    WAIT = 24,

    /// <summary>
    ///     Address bus
    /// </summary>
    /// <remark>Output</remark>
    A0 = 25,

    /// <summary>
    ///     Address bus
    /// </summary>
    /// <remark>Output</remark>
    A1 = 26,

    /// <summary>
    ///     Address bus
    /// </summary>
    /// <remark>Output</remark>
    A2 = 27,

    /// <summary>
    ///     The +12 V power supply. This must be the last connected and first disconnected power source.
    /// </summary>
    /// <remark>—</remark>
    V12 = 28,

    /// <summary>
    ///     The address bus; can switch into high impedance state on demand
    /// </summary>
    /// <remark>Output</remark>
    A3 = 29,

    /// <summary>
    ///     The address bus; can switch into high impedance state on demand
    /// </summary>
    /// <remark>Output</remark>
    A4 = 30,

    /// <summary>
    ///     The address bus; can switch into high impedance state on demand
    /// </summary>
    /// <remark>Output</remark>
    A5 = 31,

    /// <summary>
    ///     The address bus; can switch into high impedance state on demand
    /// </summary>
    /// <remark>Output</remark>
    A6 = 32,

    /// <summary>
    ///     The address bus; can switch into high impedance state on demand
    /// </summary>
    /// <remark>Output</remark>
    A7 = 33,

    /// <summary>
    ///     The address bus; can switch into high impedance state on demand
    /// </summary>
    /// <remark>Output</remark>
    A8 = 34,

    /// <summary>
    ///     The address bus; can switch into high impedance state on demand
    /// </summary>
    /// <remark>Output</remark>
    A9 = 35,

    /// <summary>
    ///     The address bus; can switch into high impedance state on demand
    /// </summary>
    /// <remark>Output</remark>
    A15 = 36,

    /// <summary>
    ///     The address bus; can switch into high impedance state on demand
    /// </summary>
    /// <remark>Output</remark>
    A12 = 37,

    /// <summary>
    ///     The address bus; can switch into high impedance state on demand
    /// </summary>
    /// <remark>Output</remark>
    A13 = 38,

    /// <summary>
    ///     The address bus; can switch into high impedance state on demand
    /// </summary>
    /// <remark>Output</remark>
    A14 = 39,

    /// <summary>
    ///     The address bus; can switch into high impedance state on demand
    /// </summary>
    /// <remark>Output</remark>
    A11 = 40
}