//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="DyaVan Production">
//     Copyright (c) DyaVan Production. All rights reserved.
// </copyright>
// <author>John Doe</author>
//-----------------------------------------------------------------------

using System;

[assembly: CLSCompliant(true)]

namespace MyFtpServer
{
    public static class Program
    {
        public static void Main()
        {
            FtpServer ftpServer = new FtpServer();
            ftpServer.Start();
            Console.ReadKey();
        }
    }
}
