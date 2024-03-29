# Generated by the gRPC Python protocol compiler plugin. DO NOT EDIT!
"""Client and server classes corresponding to protobuf-defined services."""
import grpc

import Patron_pb2 as Patron__pb2
from google.protobuf import empty_pb2 as google_dot_protobuf_dot_empty__pb2


class PatronStub(object):
    """Missing associated documentation comment in .proto file."""

    def __init__(self, channel):
        """Constructor.

        Args:
            channel: A grpc.Channel.
        """
        self.Auth = channel.unary_unary(
                '/Patron/Auth',
                request_serializer=Patron__pb2.AuthRequest.SerializeToString,
                response_deserializer=Patron__pb2.BooleanMessageReply.FromString,
                )
        self.ResetPassword = channel.unary_unary(
                '/Patron/ResetPassword',
                request_serializer=Patron__pb2.ResetPasswordRequest.SerializeToString,
                response_deserializer=Patron__pb2.BooleanMessageReply.FromString,
                )
        self.BeginShutdown = channel.unary_unary(
                '/Patron/BeginShutdown',
                request_serializer=Patron__pb2.BeginShutdownRequest.SerializeToString,
                response_deserializer=Patron__pb2.BooleanMessageReply.FromString,
                )
        self.TotalUserCount = channel.unary_unary(
                '/Patron/TotalUserCount',
                request_serializer=google_dot_protobuf_dot_empty__pb2.Empty.SerializeToString,
                response_deserializer=Patron__pb2.UserCountReply.FromString,
                )
        self.IsShutdownComplete = channel.unary_unary(
                '/Patron/IsShutdownComplete',
                request_serializer=google_dot_protobuf_dot_empty__pb2.Empty.SerializeToString,
                response_deserializer=Patron__pb2.BooleanMessageReply.FromString,
                )


class PatronServicer(object):
    """Missing associated documentation comment in .proto file."""

    def Auth(self, request, context):
        """Missing associated documentation comment in .proto file."""
        context.set_code(grpc.StatusCode.UNIMPLEMENTED)
        context.set_details('Method not implemented!')
        raise NotImplementedError('Method not implemented!')

    def ResetPassword(self, request, context):
        """Missing associated documentation comment in .proto file."""
        context.set_code(grpc.StatusCode.UNIMPLEMENTED)
        context.set_details('Method not implemented!')
        raise NotImplementedError('Method not implemented!')

    def BeginShutdown(self, request, context):
        """Missing associated documentation comment in .proto file."""
        context.set_code(grpc.StatusCode.UNIMPLEMENTED)
        context.set_details('Method not implemented!')
        raise NotImplementedError('Method not implemented!')

    def TotalUserCount(self, request, context):
        """Missing associated documentation comment in .proto file."""
        context.set_code(grpc.StatusCode.UNIMPLEMENTED)
        context.set_details('Method not implemented!')
        raise NotImplementedError('Method not implemented!')

    def IsShutdownComplete(self, request, context):
        """Missing associated documentation comment in .proto file."""
        context.set_code(grpc.StatusCode.UNIMPLEMENTED)
        context.set_details('Method not implemented!')
        raise NotImplementedError('Method not implemented!')


def add_PatronServicer_to_server(servicer, server):
    rpc_method_handlers = {
            'Auth': grpc.unary_unary_rpc_method_handler(
                    servicer.Auth,
                    request_deserializer=Patron__pb2.AuthRequest.FromString,
                    response_serializer=Patron__pb2.BooleanMessageReply.SerializeToString,
            ),
            'ResetPassword': grpc.unary_unary_rpc_method_handler(
                    servicer.ResetPassword,
                    request_deserializer=Patron__pb2.ResetPasswordRequest.FromString,
                    response_serializer=Patron__pb2.BooleanMessageReply.SerializeToString,
            ),
            'BeginShutdown': grpc.unary_unary_rpc_method_handler(
                    servicer.BeginShutdown,
                    request_deserializer=Patron__pb2.BeginShutdownRequest.FromString,
                    response_serializer=Patron__pb2.BooleanMessageReply.SerializeToString,
            ),
            'TotalUserCount': grpc.unary_unary_rpc_method_handler(
                    servicer.TotalUserCount,
                    request_deserializer=google_dot_protobuf_dot_empty__pb2.Empty.FromString,
                    response_serializer=Patron__pb2.UserCountReply.SerializeToString,
            ),
            'IsShutdownComplete': grpc.unary_unary_rpc_method_handler(
                    servicer.IsShutdownComplete,
                    request_deserializer=google_dot_protobuf_dot_empty__pb2.Empty.FromString,
                    response_serializer=Patron__pb2.BooleanMessageReply.SerializeToString,
            ),
    }
    generic_handler = grpc.method_handlers_generic_handler(
            'Patron', rpc_method_handlers)
    server.add_generic_rpc_handlers((generic_handler,))


 # This class is part of an EXPERIMENTAL API.
class Patron(object):
    """Missing associated documentation comment in .proto file."""

    @staticmethod
    def Auth(request,
            target,
            options=(),
            channel_credentials=None,
            call_credentials=None,
            insecure=False,
            compression=None,
            wait_for_ready=None,
            timeout=None,
            metadata=None):
        return grpc.experimental.unary_unary(request, target, '/Patron/Auth',
            Patron__pb2.AuthRequest.SerializeToString,
            Patron__pb2.BooleanMessageReply.FromString,
            options, channel_credentials,
            insecure, call_credentials, compression, wait_for_ready, timeout, metadata)

    @staticmethod
    def ResetPassword(request,
            target,
            options=(),
            channel_credentials=None,
            call_credentials=None,
            insecure=False,
            compression=None,
            wait_for_ready=None,
            timeout=None,
            metadata=None):
        return grpc.experimental.unary_unary(request, target, '/Patron/ResetPassword',
            Patron__pb2.ResetPasswordRequest.SerializeToString,
            Patron__pb2.BooleanMessageReply.FromString,
            options, channel_credentials,
            insecure, call_credentials, compression, wait_for_ready, timeout, metadata)

    @staticmethod
    def BeginShutdown(request,
            target,
            options=(),
            channel_credentials=None,
            call_credentials=None,
            insecure=False,
            compression=None,
            wait_for_ready=None,
            timeout=None,
            metadata=None):
        return grpc.experimental.unary_unary(request, target, '/Patron/BeginShutdown',
            Patron__pb2.BeginShutdownRequest.SerializeToString,
            Patron__pb2.BooleanMessageReply.FromString,
            options, channel_credentials,
            insecure, call_credentials, compression, wait_for_ready, timeout, metadata)

    @staticmethod
    def TotalUserCount(request,
            target,
            options=(),
            channel_credentials=None,
            call_credentials=None,
            insecure=False,
            compression=None,
            wait_for_ready=None,
            timeout=None,
            metadata=None):
        return grpc.experimental.unary_unary(request, target, '/Patron/TotalUserCount',
            google_dot_protobuf_dot_empty__pb2.Empty.SerializeToString,
            Patron__pb2.UserCountReply.FromString,
            options, channel_credentials,
            insecure, call_credentials, compression, wait_for_ready, timeout, metadata)

    @staticmethod
    def IsShutdownComplete(request,
            target,
            options=(),
            channel_credentials=None,
            call_credentials=None,
            insecure=False,
            compression=None,
            wait_for_ready=None,
            timeout=None,
            metadata=None):
        return grpc.experimental.unary_unary(request, target, '/Patron/IsShutdownComplete',
            google_dot_protobuf_dot_empty__pb2.Empty.SerializeToString,
            Patron__pb2.BooleanMessageReply.FromString,
            options, channel_credentials,
            insecure, call_credentials, compression, wait_for_ready, timeout, metadata)
