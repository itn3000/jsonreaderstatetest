﻿// See https://aka.ms/new-console-template for more information
using System.Reflection;
using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);