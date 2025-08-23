import Tippy from '@tippyjs/react';
import 'tippy.js/dist/tippy.css'; // optional for styling

const Tooltip = ({ content, children }) => {
  return (
    <Tippy content={content} placement="top" arrow={true} animation="shift-away" theme="translucent">
      {children}
    </Tippy>
  );
};

export default Tooltip;
