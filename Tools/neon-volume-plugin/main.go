package main

import (
	"io/ioutil"
	"log"
	"os"
	
	"github.com/docker/go-plugins-helpers/volume"
)

const socketAddress = "/run/docker/plugins/neon-volume.sock"

type neonDriver struct {

	// Perhaps we'll need some device state
	// here in the future.
}

type errorString struct {

    s string
}

func (e *errorString) Error() string {

    return e.s
}

func toError(text string) error {

    return &errorString{text}
}

func mountPath(volumeName string) string {

	return "/cfs/docker/" + volumeName
}

func cfsReady() (bool, error) {

	stat, err := os.Stat("/cfs/READY")

	if (err == nil && !stat.IsDir()) {
		return true, nil
	} else {
		log.Println("[/cfs] is not ready.");
		return false, cfsNotReady()
	}
}

func volumeExists(volumeName string) bool {

	path      := mountPath(volumeName)
	stat, err := os.Stat(path)

	if (err == nil && stat.IsDir()) {
		return true
	} else {	
		return false
	}
}

func cfsNotReady() error {

	return toError("Cluster distributed filesystem [/cfs] is not ready.")
}

func volumeDoesNotExist(volumeName string) error {

	return toError("volume [" + volumeName + "] does not exist.")
}

func (driver *neonDriver) Create(request *volume.CreateRequest) error {

	log.Println("create:", request.Name);

	ready, error := cfsReady()

	if (!ready) {
		return error
	}
	
	if (volumeExists(request.Name)) {

		// I'm not going to treat this as an error since CFS is
		// distributed and its likely that folks may have 
		// already created the volume on another host.

		log.Println("volume [" + request.Name + "] already exists.")
		return nil
	}

	error = os.MkdirAll(mountPath(request.Name), 770)
	if (error != nil) {
		log.Println("create error:", error)
	}

	return error;
}

func (driver *neonDriver) Remove(request *volume.RemoveRequest) error {

	log.Println("remove:", request.Name);

	ready, error := cfsReady()

	if (!ready) {
		return error
	}
	
	if (volumeExists(request.Name)) {

		error = os.RemoveAll(mountPath(request.Name))
		if (error != nil) {
			log.Println("remove error:", error)
		}

		return error

	} else {
		return toError("volume [" + request.Name + "] does not exist.")
	}
}

func (driver *neonDriver) Path(request *volume.PathRequest) (*volume.PathResponse, error) {

	log.Println("path:", request.Name);

	ready, error := cfsReady()

	if (!ready) {
		return nil, error
	}
	
	if (volumeExists(request.Name)) {
		return &volume.PathResponse{Mountpoint: mountPath(request.Name)}, nil
	} else {
		return nil, volumeDoesNotExist(request.Name)
	}
}

func (driver *neonDriver) Mount(request *volume.MountRequest) (*volume.MountResponse, error) {

	log.Println("mount:", request.Name);

	ready, error := cfsReady()

	if (!ready) {
		return nil, error
	}
	
	if (volumeExists(request.Name)) {
		return &volume.MountResponse{Mountpoint: mountPath(request.Name)}, nil
	} else {
		return nil, volumeDoesNotExist(request.Name)
	}
}

func (driver *neonDriver) Unmount(request *volume.UnmountRequest) error {

	log.Println("unmount:", request.Name);

	ready, error := cfsReady()

	if (!ready) {
		return error
	}
	
	if (volumeExists(request.Name)) {
		return nil
	} else {
		return volumeDoesNotExist(request.Name)
	}
}

func (driver *neonDriver) Get(request *volume.GetRequest) (*volume.GetResponse, error) {

	log.Println("get:", request.Name);

	ready, error := cfsReady()

	if (!ready) {
		return nil, error
	}
	
	if (volumeExists(request.Name)) {
		return &volume.GetResponse{Volume: &volume.Volume{Name: request.Name, Mountpoint: mountPath(request.Name)}}, nil
	} else {
		return nil, volumeDoesNotExist(request.Name)
	}
}

func (driver *neonDriver) List() (*volume.ListResponse, error) {

	log.Println("list");

	ready, error := cfsReady()

	if (!ready) {
		return nil, error;
	}
	
	var volumes []*volume.Volume

	files, err := ioutil.ReadDir("/cfs/docker")

	if err == nil {

		for _, file := range files {

			if (file.IsDir()) {
			
				volumes = append(volumes, &volume.Volume{Name: file.Name(), Mountpoint: mountPath(file.Name())})
			}
		}
	}

	return &volume.ListResponse{Volumes: volumes}, nil
}

func (driver *neonDriver) Capabilities() *volume.CapabilitiesResponse {

	log.Println("capabilities");

	return &volume.CapabilitiesResponse{Capabilities: volume.Capability{Scope: "local"}}
}

func main() {

	log.Println("Starting Docker [neon-volume] driver plugin")

	driver  := &neonDriver{ }
	handler := volume.NewHandler(driver)

	handler.ServeUnix(socketAddress, 0)
}
