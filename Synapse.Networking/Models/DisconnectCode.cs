using System;

namespace Synapse.Networking.Models;

public enum DisconnectCode
{
    UnexpectedException,
    ConnectionClosedUnexpectedly,
    DisconnectedByUser,
    ClientDisposed,
    ServerClosing,
    DuplicateConnection,
    MaximumConnections,
    RateLimited,
    Unauthenticated,
    Banned,
    NotWhitelisted,
    ListingMismatch
}

public static class DisconnectCodeExtensions
{
    public static string ToReason(this DisconnectCode code)
    {
        return code switch
        {
            DisconnectCode.UnexpectedException => "An unexpected exception has occurred",
            DisconnectCode.ConnectionClosedUnexpectedly => "Connection closed unexpectedly",
            DisconnectCode.DisconnectedByUser => "Disconnect by user",
            DisconnectCode.ClientDisposed => "Client was disposed",
            DisconnectCode.ServerClosing => "Server closing",
            DisconnectCode.DuplicateConnection => "Connected from another client",
            DisconnectCode.MaximumConnections => "Server is full",
            DisconnectCode.RateLimited => "Too many connections",
            DisconnectCode.Unauthenticated => "Failed to authenticate, try restarting your game",
            DisconnectCode.Banned => "Banned",
            DisconnectCode.NotWhitelisted => "Not whitelisted",
            DisconnectCode.ListingMismatch => "Listing mismatch, try rejoining",
            _ => throw new ArgumentOutOfRangeException(nameof(code))
        };
    }
}
