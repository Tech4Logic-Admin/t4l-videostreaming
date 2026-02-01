import { Search, Video, Shield, BarChart3 } from 'lucide-react';
import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';

export default function HomePage() {
  return (
    <div className="min-h-screen bg-t4l-background">
      {/* Hero Section */}
      <section className="bg-gradient-to-r from-t4l-dark to-t4l-primary py-20">
        <div className="container mx-auto px-4 text-center">
          <h1 className="text-4xl md:text-5xl font-bold text-white mb-6">
            Enterprise Video Search
          </h1>
          <p className="text-xl text-white/80 mb-8 max-w-2xl mx-auto">
            Search through video content in any language with AI-powered transcription and
            timeline-based search. Jump directly to the moments that matter.
          </p>
          <div className="flex gap-4 justify-center">
            <Link href="/search">
              <Button size="lg" variant="secondary" className="bg-white text-t4l-primary hover:bg-gray-100">
                <Search className="mr-2 h-5 w-5" />
                Start Searching
              </Button>
            </Link>
            <Link href="/upload">
              <Button size="lg" variant="outline" className="text-white border-white hover:bg-white/10">
                <Video className="mr-2 h-5 w-5" />
                Upload Video
              </Button>
            </Link>
          </div>
        </div>
      </section>

      {/* Features Section */}
      <section className="py-16">
        <div className="container mx-auto px-4">
          <h2 className="text-3xl font-bold text-center text-t4l-dark mb-12">
            Powerful Features
          </h2>
          <div className="grid md:grid-cols-2 lg:grid-cols-4 gap-6">
            <FeatureCard
              icon={<Search className="h-10 w-10 text-t4l-primary" />}
              title="Multilingual Search"
              description="Search in any language. Our AI understands content regardless of the original language."
            />
            <FeatureCard
              icon={<Video className="h-10 w-10 text-t4l-primary" />}
              title="Timeline Navigation"
              description="Jump directly to specific moments in videos. Click any search result to start playback at that exact time."
            />
            <FeatureCard
              icon={<Shield className="h-10 w-10 text-t4l-primary" />}
              title="Enterprise Security"
              description="Role-based access control ensures users only see videos they're authorized to view."
            />
            <FeatureCard
              icon={<BarChart3 className="h-10 w-10 text-t4l-primary" />}
              title="Analytics Dashboard"
              description="Monitor usage, track content moderation, and gain insights into your video library."
            />
          </div>
        </div>
      </section>

      {/* Footer */}
      <footer className="bg-t4l-dark text-white py-8">
        <div className="container mx-auto px-4 text-center">
          <p className="text-white/60">
            (c) {new Date().getFullYear()} Tech4Logic. All rights reserved.
          </p>
        </div>
      </footer>
    </div>
  );
}

function FeatureCard({
  icon,
  title,
  description,
}: {
  icon: React.ReactNode;
  title: string;
  description: string;
}) {
  return (
    <Card className="text-center hover:shadow-lg transition-shadow">
      <CardHeader>
        <div className="flex justify-center mb-4">{icon}</div>
        <CardTitle className="text-t4l-dark">{title}</CardTitle>
      </CardHeader>
      <CardContent>
        <CardDescription>{description}</CardDescription>
      </CardContent>
    </Card>
  );
}
