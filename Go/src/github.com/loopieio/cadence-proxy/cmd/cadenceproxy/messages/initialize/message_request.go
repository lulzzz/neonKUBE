package initialize

import (
	"fmt"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"
)

type (

	// InitializeRequest is a ProxyRequest of MessageType
	// InitializeRequest.  It holds a reference to a
	// ProxyRequest in memory
	InitializeRequest struct {
		*base.ProxyRequest
	}
)

// InitInitialize is a method that adds a key/value entry into the
// IntToMessageStruct at keys InitializeRequest and InitializeReply.
// The values are new instances of a InitializeRequest and InitializeReply
func InitInitialize() {
	key := int(messages.InitializeRequest)
	base.IntToMessageStruct[key] = NewInitializeRequest()

	key = int(messages.InitializeReply)
	base.IntToMessageStruct[key] = NewInitializeReply()
}

// NewInitializeRequest is the default constructor for a InitializeRequest
//
// returns *InitializeRequest -> pointer to a newly initialized
// InitializeRequest in memory
func NewInitializeRequest() *InitializeRequest {
	request := new(InitializeRequest)
	request.ProxyRequest = base.NewProxyRequest()
	request.Type = messages.InitializeRequest
	return request
}

// GetLibraryAddress gets the LibraryAddress property from an InitializeRequest
// in its properties map
//
// returns *string -> a pointer to a string in memory that holds the value
// of an InitializeRequest's LibraryAddress
func (request *InitializeRequest) GetLibraryAddress() *string {
	return request.GetStringProperty(base.LibraryAddressKey)
}

// SetLibraryAddress sets the LibraryAddress property in an INitializeRequest's
// properties map
//
// param value *string -> a pointer to a string that holds the LibraryAddress value
// to set in the request's properties map
func (request *InitializeRequest) SetLibraryAddress(value *string) {
	request.SetStringProperty(base.LibraryAddressKey, value)
}

// GetLibraryPort gets the LibraryPort property from an InitializeRequest
// in its properties map
//
// returns *string -> a pointer to a string in memory that holds the value
// of an InitializeRequest's LibraryPort
func (request *InitializeRequest) GetLibraryPort() *string {
	return request.GetStringProperty(base.LibraryPortKey)
}

// SetLibraryPort sets the LibraryPort property in an INitializeRequest's
// properties map
//
// param value *string -> a pointer to a string that holds the LibraryPort value
// to set in the request's properties map
func (request *InitializeRequest) SetLibraryPort(value *string) {
	request.SetStringProperty(base.LibraryPortKey, value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (request *InitializeRequest) Clone() base.IProxyMessage {
	initializeRequest := NewInitializeRequest()
	var messageClone base.IProxyMessage = initializeRequest
	request.CopyTo(messageClone)
	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (request *InitializeRequest) CopyTo(target base.IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	v, ok := target.(*InitializeRequest)
	if ok {
		v.SetLibraryAddress(request.GetLibraryAddress())
		v.SetLibraryPort(request.GetLibraryPort())
		v.SetProxyRequest(request.ProxyRequest)
	}
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (request *InitializeRequest) SetProxyMessage(value *base.ProxyMessage) {
	*request.ProxyMessage = *value
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (request *InitializeRequest) GetProxyMessage() *base.ProxyMessage {
	return request.ProxyMessage
}

// String inherits docs from ProxyMessage.String()
func (request *InitializeRequest) String() string {
	str := ""
	str = fmt.Sprintf("%s\n{\n", str)
	str = fmt.Sprintf("%s%s", str, request.ProxyRequest.String())
	str = fmt.Sprintf("%s}\n", str)
	return str
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetProxyRequest inherits docs from ProxyRequest.GetProxyRequest()
func (request *InitializeRequest) GetProxyRequest() *base.ProxyRequest {
	return request.ProxyRequest
}

// SetProxyRequest inherits docs from ProxyRequest.SetProxyRequest()
func (request *InitializeRequest) SetProxyRequest(value *base.ProxyRequest) {
	*request.ProxyRequest = *value
}