﻿using System;
using System.Collections.Generic;
using System.Text;

namespace IO.Ably.Shared
{
    public interface INowProvider
    {
        DateTimeOffset Now();
    }
}