import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Toaster } from '@/components/ui/toaster';
import { TooltipProvider } from '@/components/ui/tooltip';
import { Route, Switch, Router as WouterRouter } from 'wouter';
import { AppLayout } from '@/components/layout/AppLayout';
import Dashboard from '@/pages/Dashboard';
import MatchSetup from '@/pages/MatchSetup';
import MatchDetail from '@/pages/MatchDetail';
import Cameras from '@/pages/Cameras';
import LiveCapture from '@/pages/LiveCapture';
import Events from '@/pages/Events';
import StreamDeck from '@/pages/StreamDeck';
import Overlays from '@/pages/Overlays';
import Streaming from '@/pages/Streaming';
import Health from '@/pages/Health';
import Exports from '@/pages/Exports';
import Settings from '@/pages/Settings';
import NotFound from '@/pages/not-found';

const queryClient = new QueryClient();

function Router() {
  return (
    <AppLayout>
      <Switch>
        <Route path="/" component={Dashboard} />
        <Route path="/match/new" component={MatchSetup} />
        <Route path="/match/:matchId" component={MatchDetail} />
        <Route path="/cameras" component={Cameras} />
        <Route path="/capture" component={LiveCapture} />
        <Route path="/events/:matchId" component={Events} />
        <Route path="/events/current" component={Events} />
        <Route path="/stream-deck" component={StreamDeck} />
        <Route path="/overlays" component={Overlays} />
        <Route path="/streaming" component={Streaming} />
        <Route path="/health" component={Health} />
        <Route path="/exports" component={Exports} />
        <Route path="/settings" component={Settings} />
        <Route component={NotFound} />
      </Switch>
    </AppLayout>
  );
}

function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <TooltipProvider>
        <WouterRouter base={import.meta.env.BASE_URL.replace(/\/$/, '')}>
          <Router />
        </WouterRouter>
        <Toaster />
      </TooltipProvider>
    </QueryClientProvider>
  );
}

export default App;
