﻿using PKHeX.Core;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SysBot.Pokemon
{
    public class Idle : PokeRoutineExecutor
    {
        public Idle(PokeBotState cfg) : base(cfg) { }


        public override async Task MainLoop(CancellationToken token)
        {
            Log("Nothing to do!");
            while (!token.IsCancellationRequested) { }
        }
    }

}