using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;

namespace DiffieHellmanMerkle
{

    class Program
    {

        const int applicationPort = 10000;

        static Random random;

        static ulong privateNumber;
        static ulong publicPrivateNumber;
        static ulong otherPublicPrivateNumber;
        static ulong sharedSecretNumber;

        /*
         * Process:
         * 1) Both generate private numbers
         * 2) Server sends public numbers
         * 3) Both generate public-private numbers
         * 4) Server sends public-private number
         * 5) Client sends public-private number
         * 6) Both generate shared secret number
         */

        static void Main(string[] args)
        {

            string hostname = args[0];
            bool isServer = GetBoolFromConsoleArg(args[1]);

            random = new Random();

            IPAddress endpointIpAddress = Dns.GetHostEntry(hostname).AddressList[0];
            IPEndPoint endpoint = new IPEndPoint(endpointIpAddress, applicationPort);

            try
            {

                Socket socket;

                #region Set Up Socket

                Console.WriteLine("Setting up socket...");

                if (isServer)
                {

                    Socket listener = new Socket(endpoint.AddressFamily,
                        SocketType.Stream,
                        ProtocolType.Tcp);

                    //Bind listener

                    listener.Bind(endpoint);
                    listener.Listen(1);

                    //Accept client connection

                    Console.WriteLine("Awaiting connection...");
                    socket = listener.Accept();
                    Console.WriteLine("Connected");

                }
                else
                {

                    socket = new Socket(endpoint.AddressFamily,
                        SocketType.Stream,
                        ProtocolType.Tcp);

                    socket.Connect(endpoint);

                }

                Console.WriteLine("Socket set-up");

                #endregion

                #region Generate Private Number

                privateNumber = GeneratePrivateNumber();

                Console.WriteLine($"Private number generated ({privateNumber})");

                #endregion

                #region Public Numbers

                PublicNumbers publicNumbers;

                if (isServer)
                {

                    //Generate public numbers

                    publicNumbers = PublicNumbers.GeneratePublicNumbers(); //Test and return to PublicNumbers.ChoosePublicNumbers if doesn't work

                    //Get byte array of public numbers

                    byte[] publicNumberBytes = publicNumbers.ToByteArray();

                    //Send public numbers

                    socket.Send(publicNumberBytes);

                    Console.WriteLine($"Public numbers ({publicNumbers.modSize},{publicNumbers.baseNumber}) sent");

                }
                else
                {

                    byte[] publicNumberBytes = new byte[16];
                    socket.Receive(publicNumberBytes, 16, SocketFlags.None);

                    publicNumbers = new PublicNumbers(publicNumberBytes);

                    Console.WriteLine($"Public numbers ({publicNumbers.modSize},{publicNumbers.baseNumber}) received");

                }

                #endregion

                #region Generating Public-Private Number

                Console.WriteLine("Generating public-private number...");

                publicPrivateNumber = GeneratePublicPrivateNumber(privateNumber,
                    publicNumbers.modSize,
                    publicNumbers.baseNumber);

                Console.WriteLine($"Public-private number generated ({publicPrivateNumber})");

                #endregion

                #region Send/Receive Public-Private Numbers

                byte[] publicPrivateNumberBytes = BitConverter.GetBytes(publicPrivateNumber);

                if (isServer)
                {

                    SendPublicPrivateNumber(socket, publicPrivateNumberBytes);
                    Console.WriteLine("Public-private number sent");

                    otherPublicPrivateNumber = ReceivePublicPrivateNumber(socket);
                    Console.WriteLine($"Other public-private number received ({otherPublicPrivateNumber})");

                }
                else
                {

                    otherPublicPrivateNumber = ReceivePublicPrivateNumber(socket);
                    Console.WriteLine($"Other public-private number received ({otherPublicPrivateNumber})");

                    SendPublicPrivateNumber(socket, publicPrivateNumberBytes);
                    Console.WriteLine("Public-private number sent");

                }

                #endregion

                #region Generate Shared Secret

                sharedSecretNumber = otherPublicPrivateNumber * publicPrivateNumber;
                Console.WriteLine("Shared secret number: " + sharedSecretNumber);

                #endregion

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();

        }

        private static readonly string[] falseConsoleArgs = new string[]
        {
            "false",
            "0",
            "no",
            ""
        };
        static bool GetBoolFromConsoleArg(string arg)
        {

            if (falseConsoleArgs.Contains(arg.ToLower()))
            {
                return false;
            }
            else
            {
                return true;
            }

        }

        static void SendPublicPrivateNumber(Socket socket,
            byte[] numberBytes)
        {

            socket.Send(numberBytes);

        }

        static ulong ReceivePublicPrivateNumber(Socket socket)
        {

            byte[] bytes = new byte[8]; //ulong is 8 bytes
            socket.Receive(bytes, 8, SocketFlags.None);

            return BitConverter.ToUInt64(bytes, 0);

        }

        static ulong GeneratePrivateNumber()
        {
            return Convert.ToUInt64(random.Next());
        }

        static ulong GeneratePublicPrivateNumber(ulong privateNumber,
            ulong modSize,
            ulong baseNumber)
        {

            ulong currentRemainder = baseNumber;

            for (ulong i = 0; i < privateNumber; i++)
            {

                currentRemainder *= baseNumber;
                currentRemainder %= modSize;

            }

            return currentRemainder;

        }

    }

    struct PublicNumbers
    {

        public ulong modSize;
        public ulong baseNumber;

        public PublicNumbers(byte[] receivedBytes)
        {

            byte[] modSizeBytes = new byte[8];
            byte[] baseNumberBytes = new byte[8];

            Array.Copy(receivedBytes, 0, modSizeBytes, 0, 8);
            Array.Copy(receivedBytes, 8, baseNumberBytes, 0, 8);

            modSize = BitConverter.ToUInt64(modSizeBytes, 0);
            baseNumber = BitConverter.ToUInt64(baseNumberBytes, 0);

        }

        public PublicNumbers(ulong modSize,
            ulong baseNumber)
        {
            this.modSize = modSize;
            this.baseNumber = baseNumber;
        }

        public static PublicNumbers ChoosePublicNumbers()
        {

            Random random = new Random();
            int randIndex = random.Next(0, possiblePublicNumbers.Length);

            return possiblePublicNumbers[randIndex];

        }

        public static PublicNumbers GeneratePublicNumbers()
        {

            ulong modSize, baseNumber;

            //TODO: use this function instead of PublicNumbers.ChoosePublicNumbers() in main program

            /* Conditions for numbers:
             * modSize - prime
             * baseNumber - "primitive root modulo {modSize}"
             *      primitive root modulo n - exponents of {baseNumber} modulo {modSize} cycle through every int lesser than modSize
             */

            Random random = new Random();
            int randModSizeIndex = random.Next(0, hardcodedPrimes.Length);

            modSize = hardcodedPrimes[randModSizeIndex];

            List<ulong> primitiveRoots = new List<ulong>();
            for (ulong i = 1; i < modSize; i++)
            {

                if (PrimitiveRootCheck(modSize, i))
                {
                    primitiveRoots.Add(i);
                }

            }

            int randPrimitiveRootIndex = random.Next(0, primitiveRoots.Count);
            baseNumber = primitiveRoots[randPrimitiveRootIndex];

            return new PublicNumbers(modSize, baseNumber);

        }

        private static bool PrimitiveRootCheck(ulong modSize, ulong root)
        {

            //TODO: check if primitive root

            //TODO: try this: https://math.stackexchange.com/a/133720

        }

        public byte[] ToByteArray()
        {

            //Make byte arrays of public numbers. Because they are uint, each will be byte[8]

            byte[] modSizeBytes = BitConverter.GetBytes(modSize);
            byte[] baseNumberBytes = BitConverter.GetBytes(baseNumber);

            //Make byte[] of public numbers byte[]s. Because they are each byte[8], this should be byte[16]

            return modSizeBytes.Concat(baseNumberBytes).ToArray();

        }

        public static readonly ulong[] hardcodedPrimes = new ulong[]
        {
            2,
            3,
            5,
            7,
            11,
            13,
            17,
            19,
            23,
            29,
            31,
            37,
            41,
            43,
            47,
            53,
            59,
            61,
            67,
            71,
            73,
            79,
            83,
            89,
            97,
            101,
            103,
            107,
            109,
            113,
            127,
            131,
            137,
            139,
            149,
            151,
            157,
            163,
            167,
            173,
            179,
            181,
            191,
            193,
            197,
            199
        };

        public static readonly PublicNumbers[] possiblePublicNumbers = new PublicNumbers[] //Make more options (N.B. conditions)
        {
            new PublicNumbers(11, 2)
        };

    }

}