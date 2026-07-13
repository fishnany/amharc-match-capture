import React, { useState } from "react";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Label } from "@/components/ui/label";
import { 
  useGetCameras, 
  useCreateCamera, 
  useConnectCamera, 
  useDisconnectCamera,
  useTestCamera,
  useDeleteCamera,
  Camera
} from "@workspace/api-client-react";
import { toast } from "sonner";
import { Video, Plug, Unplug, Trash2, Plus, RefreshCw, Activity } from "lucide-react";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger, DialogFooter } from "@/components/ui/dialog";

export default function Cameras() {
  const { data: cameras, refetch } = useGetCameras();
  const [isAddOpen, setIsAddOpen] = useState(false);
  
  const createCam = useCreateCamera();
  const connectCam = useConnectCamera();
  const disconnectCam = useDisconnectCamera();
  const deleteCam = useDeleteCamera();
  const testCam = useTestCamera();

  const handleAdd = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const fd = new FormData(e.currentTarget);
    createCam.mutate(
      { data: {
        name: fd.get("name") as string,
        manufacturer: fd.get("manufacturer") as any,
        adapter: fd.get("adapter") as string,
        ipAddress: fd.get("ipAddress") as string,
        rtspUrl: fd.get("rtspUrl") as string,
      }},
      {
        onSuccess: () => {
          toast.success("Camera added");
          setIsAddOpen(false);
          refetch();
        }
      }
    );
  };

  return (
    <div className="p-6 md:p-8 max-w-6xl mx-auto space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-3xl font-bold tracking-tight text-white">Cameras</h2>
          <p className="text-neutral-400 mt-1">Manage PTZ cameras and video sources</p>
        </div>
        <Dialog open={isAddOpen} onOpenChange={setIsAddOpen}>
          <DialogTrigger asChild>
            <Button className="bg-white text-black hover:bg-neutral-200">
              <Plus className="w-4 h-4 mr-2" /> Add Camera
            </Button>
          </DialogTrigger>
          <DialogContent className="bg-neutral-900 border-white/10 text-white">
            <DialogHeader>
              <DialogTitle>Add New Camera</DialogTitle>
            </DialogHeader>
            <form onSubmit={handleAdd} className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label>Name</Label>
                  <Input name="name" required placeholder="e.g. Main PTZ" className="bg-black border-white/10" />
                </div>
                <div className="space-y-2">
                  <Label>Manufacturer</Label>
                  <Select name="manufacturer" defaultValue="ptzoptics">
                    <SelectTrigger className="bg-black border-white/10">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="ptzoptics">PTZOptics</SelectItem>
                      <SelectItem value="birddog">BirdDog</SelectItem>
                      <SelectItem value="panasonic">Panasonic</SelectItem>
                      <SelectItem value="sony">Sony</SelectItem>
                      <SelectItem value="onvif">Generic ONVIF</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>
              <div className="space-y-2">
                <Label>Adapter</Label>
                <Select name="adapter" defaultValue="visca-over-ip">
                  <SelectTrigger className="bg-black border-white/10">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="visca-over-ip">VISCA over IP</SelectItem>
                    <SelectItem value="onvif">ONVIF Profile S/T</SelectItem>
                    <SelectItem value="http-api">HTTP API</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label>IP Address</Label>
                  <Input name="ipAddress" required placeholder="192.168.1.100" className="bg-black border-white/10" />
                </div>
                <div className="space-y-2">
                  <Label>RTSP URL</Label>
                  <Input name="rtspUrl" required placeholder="rtsp://..." className="bg-black border-white/10" />
                </div>
              </div>
              <DialogFooter className="mt-6">
                <Button type="submit" disabled={createCam.isPending} className="bg-amharc-green text-white hover:bg-amharc-green/90">
                  {createCam.isPending ? "Adding..." : "Add Camera"}
                </Button>
              </DialogFooter>
            </form>
          </DialogContent>
        </Dialog>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        {cameras?.length === 0 ? (
          <div className="col-span-full py-12 text-center border border-dashed border-white/10 rounded-lg">
            <Video className="w-12 h-12 text-neutral-600 mx-auto mb-4" />
            <p className="text-neutral-400">No cameras configured.</p>
          </div>
        ) : (
          cameras?.map((cam: Camera) => (
            <Card key={cam.cameraId} className="bg-[#0f0f0f] border-white/10">
              <CardHeader className="pb-3 flex flex-row items-start justify-between">
                <div>
                  <CardTitle className="text-xl flex items-center gap-2">
                    {cam.name}
                    {cam.connectionState === 'connected' && (
                      <span className="w-2 h-2 rounded-full bg-amharc-green" title="Connected"></span>
                    )}
                    {cam.connectionState === 'error' && (
                      <span className="w-2 h-2 rounded-full bg-destructive" title="Error"></span>
                    )}
                    {cam.connectionState === 'disconnected' && (
                      <span className="w-2 h-2 rounded-full bg-neutral-600" title="Disconnected"></span>
                    )}
                  </CardTitle>
                  <CardDescription className="text-neutral-500 font-mono mt-1">
                    {cam.manufacturer.toUpperCase()} • {cam.ipAddress}
                  </CardDescription>
                </div>
                <Button 
                  variant="ghost" 
                  size="icon" 
                  className="text-neutral-400 hover:text-destructive hover:bg-destructive/10"
                  onClick={() => {
                    if(confirm("Delete this camera?")) {
                      deleteCam.mutate({ cameraId: cam.cameraId }, { onSuccess: () => refetch() });
                    }
                  }}
                >
                  <Trash2 className="w-4 h-4" />
                </Button>
              </CardHeader>
              <CardContent>
                <div className="flex gap-2 mt-2">
                  {cam.connectionState === 'connected' ? (
                    <Button 
                      variant="outline" 
                      size="sm"
                      className="border-white/10 bg-black"
                      onClick={() => disconnectCam.mutate({ cameraId: cam.cameraId }, { onSuccess: () => refetch() })}
                      disabled={disconnectCam.isPending}
                    >
                      <Unplug className="w-4 h-4 mr-2" /> Disconnect
                    </Button>
                  ) : (
                    <Button 
                      variant="outline" 
                      size="sm"
                      className="border-white/10 bg-black text-amharc-green hover:text-amharc-green hover:bg-amharc-green/10"
                      onClick={() => connectCam.mutate({ cameraId: cam.cameraId }, { onSuccess: () => refetch() })}
                      disabled={connectCam.isPending}
                    >
                      <Plug className="w-4 h-4 mr-2" /> Connect
                    </Button>
                  )}
                  
                  <Button 
                    variant="outline" 
                    size="sm"
                    className="border-white/10 bg-black"
                    onClick={() => {
                      testCam.mutate({ cameraId: cam.cameraId }, {
                        onSuccess: (res) => {
                          if(res.success) toast.success(`Test OK: ${res.resolution}@${res.frameRate}fps (${res.latencyMs}ms)`);
                          else toast.error(`Test failed: ${res.message}`);
                        }
                      });
                    }}
                    disabled={testCam.isPending}
                  >
                    <Activity className="w-4 h-4 mr-2" /> Test Stream
                  </Button>
                </div>
                
                {cam.connectionState === 'connected' && cam.resolution && (
                  <div className="mt-4 p-3 bg-black rounded-md border border-white/5 flex justify-between text-xs font-mono text-neutral-400">
                    <span>{cam.resolution} @ {cam.frameRate}fps</span>
                    <span>{Math.round((cam.bitRate || 0)/1024)} kbps</span>
                  </div>
                )}
              </CardContent>
            </Card>
          ))
        )}
      </div>
    </div>
  );
}
