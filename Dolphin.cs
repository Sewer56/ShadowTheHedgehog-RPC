﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Reloaded.Process;
using Reloaded.Process.Memory;
using Reloaded.Process.Native;
using Reloaded_Mod_Template.Native;
using static Reloaded.Process.Native.Native;

namespace Reloaded_Mod_Template
{
    /// <summary>
    /// Helper class used to manage process memory in Dolphin emulator.
    /// </summary>
    public class Dolphin
    {
        // This is true if the emulated GameCube memory has been found.
        public bool   ValidBaseAddress { get; private set; }
        public IntPtr BaseAddress      { get; private set; }

        // This thread obtains the base address of Dolphin's emulated memory.
        private ReloadedProcess _reloadedProcess;

        // Size of emulated memory we want to find.
        private const long  EmulatedMemorySize = 0x2000000;
        private const long  EmulatedMemoryBase = 0x80000000;

        /// <summary>
        /// Creates a new instance of Dolphin emulator helper.
        /// </summary>
        public Dolphin(ReloadedProcess process)
        {
            _reloadedProcess = process;
        }

        /// <summary>
        /// Converts a Dolphin/GC Memory address in the form of 0x8XXXXXXX to a real memory address.
        /// </summary>
        public IntPtr ConvertMemoryAddress(long address)
        {
            if (!ValidBaseAddress)
                return IntPtr.Zero;

            return (IntPtr)((address - EmulatedMemoryBase) + (long)BaseAddress);
        }

        /// <summary>
        /// Loops infinitely, trying to get Dolphin's base address every approx 4 seconds.
        /// </summary>
        public void UpdateDolphinBaseAddress()
        {
            BaseAddress = GetBaseAddress(_reloadedProcess);
            ValidBaseAddress = BaseAddress != IntPtr.Zero;
        }

        /// <summary>
        /// Retrieves the base address of the GameCube emulated memory.
        /// </summary>
        /// <returns></returns>
        private unsafe IntPtr GetBaseAddress(ReloadedProcess process)
        {
            var pages = process.GetPages();

            foreach (var page in pages)
            {
                // Check if page mapped and right size.
                if (page.RegionSize == (IntPtr)EmulatedMemorySize && page.lType == PageType.Mapped)
                {
                    // Copied from Dolphin Memory Engine:

                    /*
                        Here, it's likely the right page, but it can happen that multiple pages with these criteria
                        exist and have nothing to do with the emulated memory. Only the right page has valid
                        working set information so an additional check is required that it is backed by physical
                        memory.
                    */

                    PSAPI.PSAPI_WORKING_SET_EX_INFORMATION[] setInformation = new PSAPI.PSAPI_WORKING_SET_EX_INFORMATION[1];
                    setInformation[0].VirtualAddress = page.BaseAddress;
                    bool ok = PSAPI.QueryWorkingSetEx(process.ProcessHandle, setInformation, sizeof(PSAPI.PSAPI_WORKING_SET_EX_INFORMATION) * setInformation.Length);

                    if (!ok)
                        continue;

                    // We found our address.
                    if ((setInformation[0].VirtualAttributes.Flags & 0b1) == 1)
                    {
                        return page.BaseAddress;
                    }
                }
            }

            // Not found.
            return IntPtr.Zero;
        }


    }
}
