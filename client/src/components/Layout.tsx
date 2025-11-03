import { Link, Outlet, useLocation } from 'react-router-dom';
import './Layout.css';

export default function Layout() {
  const location = useLocation();

  return (
    <div className="layout">
      <aside className="sidebar">
        <h2 className="sidebar-title">AI Examples</h2>
        <nav className="sidebar-nav">
          <Link 
            to="/example-1" 
            className={`nav-link ${location.pathname === '/example-1' ? 'active' : ''}`}
          >
            Example #1
          </Link>
          <Link 
            to="/example-2" 
            className={`nav-link ${location.pathname === '/example-2' ? 'active' : ''}`}
          >
            Example #2
          </Link>
          <Link 
            to="/example-3" 
            className={`nav-link ${location.pathname === '/example-3' ? 'active' : ''}`}
          >
            Example #3
          </Link>
          <Link 
            to="/example-4" 
            className={`nav-link ${location.pathname === '/example-4' ? 'active' : ''}`}
          >
            Example #4
          </Link>
        </nav>
      </aside>
      <main className="main-content">
        <Outlet />
      </main>
    </div>
  );
}

