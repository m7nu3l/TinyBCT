﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace TinyBCT
{
    class AsyncStubs
    {
        public static extern Task Eventually();
    }
}

class Bank
{
    public class State
    {
        public State()
        {
            balance = 0;
            withdraw = false;
            deposit = false;
        }

        public int balance;
        public bool withdraw;
        public bool deposit;
    }

    public static async Task Withdraw(int i, State state)
    {
        int temp;
        temp = state.balance;
        temp = temp - i;
        await TinyBCT.AsyncStubs.Eventually();
        state.balance = temp;
        state.withdraw = true;
        Contract.Assert(!state.deposit);
   }

    public static async Task Deposit(int i, State state)
    {
        int temp;
        temp = state.balance;
        temp = temp + i;
        state.balance = temp;
        state.deposit = true;
        await TinyBCT.AsyncStubs.Eventually();
    }

    public static async Task Test_Interleaved_Transaction4(int i)
    {
        State state = new State();
        var t_withdraw = Withdraw(i, state);
        var t_deposit = Deposit(i, state);
        await t_withdraw;
        await t_deposit;
    }
}