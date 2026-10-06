import { ArrowLeft } from 'lucide-react';
import icon from '../assets/icon-128.png';
import './bits.css';
import './SubpageHeader.css';

/** The top of the site's other pages (release notes, privacy, code signing):the name, and a way back to the front page. */
export function SubpageHeader() {
  return (
    <header className="sub-header">
      <div className="page sub-header-inner">
        <a className="sub-brand" href="../">
          <img src={icon} alt="" width={30} height={30} />
          <span>Language Autocorrect</span>
        </a>
        <a className="btn btn-quiet sub-back" href="../"><ArrowLeft size={16} strokeWidth={2.2} aria-hidden /> Back to the site</a>
      </div>
    </header>
  );
}
