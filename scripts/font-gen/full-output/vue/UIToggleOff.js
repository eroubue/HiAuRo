import { defineComponent, h } from 'vue';

export const UIToggleOff = defineComponent({
  name: 'UIToggleOff',
  props: {
    class: {
      type: String,
      default: ''
    }
  },
  setup(props, { attrs }) {
    return () => h(
      'svg',
      {
        viewBox: '0 0 20 20',
        
        class: `game-icons ${props.class}`,
        ...attrs
      },
      [
        h('path', {"d": "M6.19971 3C7.30428 3 8.19971 3.89543 8.19971 5C8.1995 6.10439 7.30415 7 6.19971 7H3.80029C2.69585 7 1.8005 6.10439 1.80029 5C1.80029 3.89543 2.69572 3 3.80029 3H6.19971ZM3.99951 3.69922C3.2817 3.6994 2.69971 4.28214 2.69971 5C2.69993 5.71767 3.28184 6.29962 3.99951 6.2998C4.71734 6.2998 5.30007 5.71778 5.30029 5C5.30029 4.28203 4.71748 3.69922 3.99951 3.69922Z", "fillRule": "evenodd"})
      ]
    );
  }
});
