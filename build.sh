#!/usr/bin/env bash

dotnet tool restore
dotnet tool run fake run ./build.fsx $@
