import { Link } from "wouter";
import { AlertCircle } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";

export default function NotFound() {
  return (
    <div className="min-h-screen w-full flex items-center justify-center bg-black">
      <Card className="w-full max-w-md mx-4 bg-[#0f0f0f] border-white/10">
        <CardContent className="pt-6">
          <div className="flex mb-4 gap-2 text-destructive font-mono">
            <AlertCircle className="h-6 w-6" />
            <h1 className="text-xl font-bold uppercase tracking-widest">404 Error</h1>
          </div>

          <p className="mt-4 text-sm text-neutral-400 font-mono mb-8">
            The requested interface panel could not be found. 
            Check routing configuration or return to the main dashboard.
          </p>
          
          <Link href="/" className="inline-flex items-center justify-center rounded-md text-sm font-medium ring-offset-background transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50 h-10 px-4 py-2 w-full bg-white text-black hover:bg-neutral-200">
            Return to Dashboard
          </Link>
        </CardContent>
      </Card>
    </div>
  );
}
